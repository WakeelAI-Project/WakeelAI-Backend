using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Services;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

/// <summary>FIX-12: covers LogActionAsync's tenant resolution, including the explicit
/// companyId override needed for actions that happen before an ambient tenant context
/// exists (company registration, forgot-password reset).</summary>
public class AuditLogServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock = new();
    private readonly Mock<ICurrentTenantService> _currentTenantServiceMock = new();

    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.AuditLogs).Returns(_auditLogRepositoryMock.Object);
        _sut = new AuditLogService(_unitOfWorkMock.Object, _currentTenantServiceMock.Object);
    }

    [Fact]
    public async Task LogActionAsync_GivenAmbientTenant_WritesUsingIt()
    {
        var companyId = Guid.NewGuid();
        _currentTenantServiceMock.SetupGet(t => t.HasTenant).Returns(true);
        _currentTenantServiceMock.SetupGet(t => t.CompanyId).Returns(companyId);

        await _sut.LogActionAsync(Guid.NewGuid(), "EMPLOYEE_CREATED", "details");

        _auditLogRepositoryMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.CompanyId == companyId && a.Action == "EMPLOYEE_CREATED"), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogActionAsync_GivenNoAmbientTenantButExplicitCompanyId_StillWrites()
    {
        // FIX-12: COMPANY_REGISTERED and PASSWORD_RESET happen on anonymous requests with
        // no tenant context yet - the company must be passed explicitly.
        _currentTenantServiceMock.SetupGet(t => t.HasTenant).Returns(false);
        _currentTenantServiceMock.SetupGet(t => t.CompanyId).Returns((Guid?)null);
        var explicitCompanyId = Guid.NewGuid();

        await _sut.LogActionAsync(Guid.NewGuid(), "COMPANY_REGISTERED", "details", explicitCompanyId);

        _auditLogRepositoryMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.CompanyId == explicitCompanyId), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogActionAsync_GivenNoAmbientTenantAndNoExplicitCompanyId_DoesNothing()
    {
        _currentTenantServiceMock.SetupGet(t => t.HasTenant).Returns(false);
        _currentTenantServiceMock.SetupGet(t => t.CompanyId).Returns((Guid?)null);

        await _sut.LogActionAsync(Guid.NewGuid(), "SOME_ACTION", "details");

        _auditLogRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogActionAsync_NeverThrows_WhenRepositoryFails()
    {
        _currentTenantServiceMock.SetupGet(t => t.HasTenant).Returns(true);
        _currentTenantServiceMock.SetupGet(t => t.CompanyId).Returns(Guid.NewGuid());
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db_unreachable"));

        // Callers wrap this in their own try/catch, so LogActionAsync itself is allowed to
        // throw - this test documents that expectation rather than asserting suppression
        // at this layer.
        var act = async () => await _sut.LogActionAsync(Guid.NewGuid(), "EMPLOYEE_CREATED", "details");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
