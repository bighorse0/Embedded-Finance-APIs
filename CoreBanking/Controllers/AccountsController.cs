using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using CoreBanking;

namespace CoreBanking.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly BankingDbContext _db;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(BankingDbContext db, ILogger<AccountsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts()
    {
        return await _db.Accounts.AsNoTracking().ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Account>> GetAccount(Guid id)
    {
        var account = await _db.Accounts.FindAsync(id);
        if (account == null) return NotFound();
        return account;
    }

    [HttpPost]
    public async Task<ActionResult<Account>> CreateAccount(Account account)
    {
        account.Id = Guid.NewGuid();
        account.CreatedAt = DateTime.UtcNow;
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        // Audit log
        _db.AccountAuditLogs.Add(new AccountAuditLog
        {
            AccountId = account.Id,
            Action = "Created",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"Account created for {account.OwnerName}"
        });
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAccount(Guid id, Account update)
    {
        var account = await _db.Accounts.FindAsync(id);
        if (account == null) return NotFound();
        account.OwnerName = update.OwnerName;
        account.Currency = update.Currency;
        account.IsActive = update.IsActive;
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        // Audit log
        _db.AccountAuditLogs.Add(new AccountAuditLog
        {
            AccountId = account.Id,
            Action = "Updated",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"Account updated for {account.OwnerName}"
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var account = await _db.Accounts.FindAsync(id);
        if (account == null) return NotFound();
        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        // Audit log
        _db.AccountAuditLogs.Add(new AccountAuditLog
        {
            AccountId = id,
            Action = "Deleted",
            PerformedBy = User.Identity?.Name ?? "system",
            Timestamp = DateTime.UtcNow,
            Details = $"Account deleted"
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }
} 