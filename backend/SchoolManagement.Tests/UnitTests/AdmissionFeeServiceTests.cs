using Moq;
using SchoolManagement.Application.DTOs.AdmissionFees;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>Admission fee per class (Masters): one fee per class name, set / cleared by Settings.Edit, suggested at Confirm.</summary>
public class AdmissionFeeServiceTests
{
    private readonly Mock<IAdmissionFeeRepository> _fees = new();
    private readonly Mock<IAdmissionRepository> _admissions = new();
    private readonly AdmissionFeeService _sut;

    public AdmissionFeeServiceTests()
    {
        _fees.Setup(f => f.GetClassNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Class 1", "Class 2" });
        _sut = new AdmissionFeeService(_fees.Object, _admissions.Object);
    }

    [Fact]
    public async Task The_list_shows_every_class_and_null_where_no_fee_is_set()
    {
        _fees.Setup(f => f.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdmissionFee> { new() { ClassName = "Class 2", Amount = 500m } });

        var list = await _sut.GetAllAsync(CancellationToken.None);

        Assert.Equal(new AdmissionFeeResponse("Class 1", null), list[0]);
        Assert.Equal(new AdmissionFeeResponse("Class 2", 500m), list[1]);
    }

    [Fact]
    public async Task Setting_a_fee_for_a_class_without_one_adds_it_rounded_to_two_decimals()
    {
        _fees.Setup(f => f.GetByClassNameAsync("Class 1", It.IsAny<CancellationToken>())).ReturnsAsync((AdmissionFee?)null);

        var result = await _sut.SetAsync(" Class 1 ", new SetAdmissionFeeRequest(250.456m), CancellationToken.None);

        Assert.Equal(250.46m, result.Amount);
        _fees.Verify(f => f.AddAsync(
            It.Is<AdmissionFee>(x => x.ClassName == "Class 1" && x.Amount == 250.46m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Setting_a_fee_again_updates_the_same_row_instead_of_adding_a_second()
    {
        var existing = new AdmissionFee { ClassName = "Class 1", Amount = 100m };
        _fees.Setup(f => f.GetByClassNameAsync("Class 1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await _sut.SetAsync("Class 1", new SetAdmissionFeeRequest(300m), CancellationToken.None);

        Assert.Equal(300m, existing.Amount);
        _fees.Verify(f => f.AddAsync(It.IsAny<AdmissionFee>(), It.IsAny<CancellationToken>()), Times.Never);
        _fees.Verify(f => f.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Setting_or_clearing_a_fee_for_a_class_that_does_not_exist_is_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.SetAsync("Class 99", new SetAdmissionFeeRequest(10m), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.ClearAsync("Class 99", CancellationToken.None));
    }

    [Fact]
    public async Task Clearing_removes_the_fee_and_clearing_a_class_with_no_fee_is_fine()
    {
        var existing = new AdmissionFee { ClassName = "Class 1", Amount = 100m };
        _fees.Setup(f => f.GetByClassNameAsync("Class 1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _fees.Setup(f => f.GetByClassNameAsync("Class 2", It.IsAny<CancellationToken>())).ReturnsAsync((AdmissionFee?)null);

        await _sut.ClearAsync("Class 1", CancellationToken.None);
        await _sut.ClearAsync("Class 2", CancellationToken.None); // nothing to remove, no error

        _fees.Verify(f => f.RemoveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _fees.Verify(f => f.RemoveAsync(It.IsAny<AdmissionFee>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_suggested_fee_is_the_fee_of_the_class_applied_for_or_null()
    {
        var admission = new Admission { AppliedForClassSection = new ClassSection { Name = "Class 2" } };
        _admissions.Setup(a => a.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(admission);
        _fees.Setup(f => f.GetByClassNameAsync("Class 2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdmissionFee { ClassName = "Class 2", Amount = 500m });

        var suggested = await _sut.GetSuggestedForAdmissionAsync(7, CancellationToken.None);
        Assert.Equal(500m, suggested.Amount);

        _fees.Setup(f => f.GetByClassNameAsync("Class 2", It.IsAny<CancellationToken>())).ReturnsAsync((AdmissionFee?)null);
        Assert.Null((await _sut.GetSuggestedForAdmissionAsync(7, CancellationToken.None)).Amount);
    }

    [Fact]
    public async Task The_suggested_fee_for_an_unknown_application_is_404()
    {
        _admissions.Setup(a => a.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Admission?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetSuggestedForAdmissionAsync(1, CancellationToken.None));
    }
}
