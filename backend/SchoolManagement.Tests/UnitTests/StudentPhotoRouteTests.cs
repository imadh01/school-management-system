using SchoolManagement.Application.Services;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class StudentPhotoRouteTests
{
    [Fact]
    public void A_stored_file_is_exposed_as_the_API_route_and_never_as_its_storage_key()
    {
        Assert.Equal("/api/students/7/photo", StudentPhotoService.RouteFor(7, "2026/10/abc.jpg"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/old-photo.jpg")]
    public void No_photo_or_an_old_web_address_means_no_photo(string? stored)
    {
        Assert.Null(StudentPhotoService.RouteFor(7, stored));
    }
}
