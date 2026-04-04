using BakeFix.Models;
using BakeFix.Services;
using Dapper;
using MySql.Data.MySqlClient;

namespace BakeFix.Repositories
{
    public class DebtRepository
    {
        private readonly string _conn;
        private readonly ITenantContext _tenant;

        public DebtRepository(IConfiguration config, ITenantContext tenant)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
            _tenant = tenant;
        }

        public async Task<IEnumerable<Debt>> GetAllAsync(string? type, bool? settled)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            return await connection.QueryAsync<Debt>(
                @"SELECT d.Id, d.OrganizationId, d.PersonName, d.Amount, d.Type,
                         d.`Date`, d.DueDate, d.Notes, d.IsSettled, d.CreatedAt,
                         COALESCE(SUM(dp.Amount), 0) AS PaidAmount
                  FROM Debts d
                  LEFT JOIN DebtPayments dp ON dp.DebtId = d.Id
                  WHERE d.OrganizationId = @orgId
                    AND (@type IS NULL OR d.Type = @type)
                    AND (@settled IS NULL OR d.IsSettled = @settled)
                  GROUP BY d.Id
                  ORDER BY d.IsSettled ASC, d.`Date` DESC, d.CreatedAt DESC",
                new { orgId, type, settled });
        }

        public async Task<Debt?> GetByIdAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var debt = await connection.QueryFirstOrDefaultAsync<Debt>(
                @"SELECT d.Id, d.OrganizationId, d.PersonName, d.Amount, d.Type,
                         d.`Date`, d.DueDate, d.Notes, d.IsSettled, d.CreatedAt,
                         COALESCE(SUM(dp.Amount), 0) AS PaidAmount
                  FROM Debts d
                  LEFT JOIN DebtPayments dp ON dp.DebtId = d.Id
                  WHERE d.Id = @id AND d.OrganizationId = @orgId
                  GROUP BY d.Id",
                new { id, orgId });

            if (debt is null) return null;

            debt.Payments = (await connection.QueryAsync<DebtPayment>(
                @"SELECT Id, DebtId, Amount, `Date`, Notes, CreatedAt
                  FROM DebtPayments
                  WHERE DebtId = @id
                  ORDER BY `Date` DESC, CreatedAt DESC",
                new { id })).ToList();

            return debt;
        }

        public async Task<Debt> CreateAsync(Debt debt)
        {
            using var connection = new MySqlConnection(_conn);
            debt.OrganizationId = _tenant.RequiredOrgId;

            await connection.ExecuteAsync(
                @"INSERT INTO Debts (Id, OrganizationId, PersonName, Amount, Type, `Date`, DueDate, Notes, IsSettled, CreatedAt)
                  VALUES (@Id, @OrganizationId, @PersonName, @Amount, @Type, @Date, @DueDate, @Notes, @IsSettled, @CreatedAt)",
                debt);

            return debt;
        }

        public async Task<bool> UpdateAsync(Debt debt)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                @"UPDATE Debts
                  SET PersonName=@PersonName, Amount=@Amount, Type=@Type,
                      `Date`=@Date, DueDate=@DueDate, Notes=@Notes
                  WHERE Id=@Id AND OrganizationId=@OrgId",
                new
                {
                    debt.Id,
                    debt.PersonName,
                    debt.Amount,
                    debt.Type,
                    debt.Date,
                    debt.DueDate,
                    debt.Notes,
                    OrgId = _tenant.RequiredOrgId
                });

            return rows > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "DELETE FROM Debts WHERE Id = @Id AND OrganizationId = @OrgId",
                new { Id = id, OrgId = _tenant.RequiredOrgId });

            return rows > 0;
        }

        public async Task<DebtPayment> AddPaymentAsync(string debtId, DebtPayment payment)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var exists = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(1) FROM Debts WHERE Id = @debtId AND OrganizationId = @orgId",
                new { debtId, orgId });

            if (exists == 0) throw new ArgumentException("Debt not found");

            await connection.ExecuteAsync(
                @"INSERT INTO DebtPayments (Id, DebtId, Amount, `Date`, Notes, CreatedAt)
                  VALUES (@Id, @DebtId, @Amount, @Date, @Notes, @CreatedAt)",
                payment);

            // Auto-settle when fully paid
            await connection.ExecuteAsync(
                @"UPDATE Debts d
                  SET d.IsSettled = CASE
                    WHEN (SELECT COALESCE(SUM(dp.Amount), 0) FROM DebtPayments dp WHERE dp.DebtId = d.Id) >= d.Amount THEN 1
                    ELSE 0
                  END
                  WHERE d.Id = @debtId",
                new { debtId });

            return payment;
        }

        public async Task<bool> DeletePaymentAsync(string debtId, string paymentId)
        {
            using var connection = new MySqlConnection(_conn);
            var orgId = _tenant.RequiredOrgId;

            var exists = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(1) FROM Debts WHERE Id = @debtId AND OrganizationId = @orgId",
                new { debtId, orgId });

            if (exists == 0) return false;

            int rows = await connection.ExecuteAsync(
                "DELETE FROM DebtPayments WHERE Id = @paymentId AND DebtId = @debtId",
                new { paymentId, debtId });

            if (rows > 0)
            {
                // Re-evaluate settled status after payment removal
                await connection.ExecuteAsync(
                    @"UPDATE Debts d
                      SET d.IsSettled = CASE
                        WHEN (SELECT COALESCE(SUM(dp.Amount), 0) FROM DebtPayments dp WHERE dp.DebtId = d.Id) >= d.Amount THEN 1
                        ELSE 0
                      END
                      WHERE d.Id = @debtId",
                    new { debtId });
            }

            return rows > 0;
        }

        public async Task<bool> SettleAsync(string id)
        {
            using var connection = new MySqlConnection(_conn);

            int rows = await connection.ExecuteAsync(
                "UPDATE Debts SET IsSettled = 1 WHERE Id = @Id AND OrganizationId = @OrgId",
                new { Id = id, OrgId = _tenant.RequiredOrgId });

            return rows > 0;
        }
    }
}
