using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBanking;
using CoreBanking.Controllers;
using SharedKernel;
using Xunit;
using FluentAssertions;
using Moq;

namespace CoreBanking.Tests;

public class AccountControllerTests
{
    private readonly BankingDbContext _context;
    private readonly AccountsController _controller;
    private readonly Mock<ILogger<AccountsController>> _loggerMock;

    public AccountControllerTests()
    {
        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BankingDbContext(options);
        _loggerMock = new Mock<ILogger<AccountsController>>();
        _controller = new AccountsController(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAccounts_ShouldReturnAllAccounts()
    {
        // Arrange
        var accounts = new List<Account>
        {
            new Account { AccountNumber = "1234567890", Type = AccountType.Checking, OwnerName = "John Doe", Currency = "USD", Balance = 1000 },
            new Account { AccountNumber = "0987654321", Type = AccountType.Savings, OwnerName = "Jane Smith", Currency = "USD", Balance = 5000 }
        };

        await _context.Accounts.AddRangeAsync(accounts);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAccounts();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedAccounts = okResult.Value.Should().BeOfType<List<Account>>().Subject;
        returnedAccounts.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAccount_WithValidId_ShouldReturnAccount()
    {
        // Arrange
        var account = new Account 
        { 
            AccountNumber = "1234567890", 
            Type = AccountType.Checking, 
            OwnerName = "John Doe", 
            Currency = "USD", 
            Balance = 1000 
        };

        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAccount(account.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedAccount = okResult.Value.Should().BeOfType<Account>().Subject;
        returnedAccount.AccountNumber.Should().Be("1234567890");
        returnedAccount.OwnerName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetAccount_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _controller.GetAccount(invalidId);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateAccount_WithValidData_ShouldCreateAccount()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "1234567890",
            Type = AccountType.Checking,
            OwnerName = "John Doe",
            Currency = "USD",
            Balance = 1000
        };

        // Act
        var result = await _controller.CreateAccount(account);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var createdAccount = createdResult.Value.Should().BeOfType<Account>().Subject;
        createdAccount.Id.Should().NotBeEmpty();
        createdAccount.AccountNumber.Should().Be("1234567890");
        createdAccount.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAccount_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var account = new Account
        {
            // Missing required fields
            Type = AccountType.Checking,
            Currency = "USD"
        };

        _controller.ModelState.AddModelError("AccountNumber", "Account number is required");

        // Act
        var result = await _controller.CreateAccount(account);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateAccount_WithValidData_ShouldUpdateAccount()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "1234567890",
            Type = AccountType.Checking,
            OwnerName = "John Doe",
            Currency = "USD",
            Balance = 1000
        };

        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();

        account.Balance = 2000;
        account.OwnerName = "John Updated";

        // Act
        var result = await _controller.UpdateAccount(account.Id, account);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var updatedAccount = await _context.Accounts.FindAsync(account.Id);
        updatedAccount.Should().NotBeNull();
        updatedAccount!.Balance.Should().Be(2000);
        updatedAccount.OwnerName.Should().Be("John Updated");
        updatedAccount.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateAccount_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "1234567890",
            Type = AccountType.Checking,
            OwnerName = "John Doe",
            Currency = "USD",
            Balance = 1000
        };

        var invalidId = Guid.NewGuid();

        // Act
        var result = await _controller.UpdateAccount(invalidId, account);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteAccount_WithValidId_ShouldDeleteAccount()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "1234567890",
            Type = AccountType.Checking,
            OwnerName = "John Doe",
            Currency = "USD",
            Balance = 1000
        };

        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteAccount(account.Id);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        
        var deletedAccount = await _context.Accounts.FindAsync(account.Id);
        deletedAccount.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAccount_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _controller.DeleteAccount(invalidId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
} 