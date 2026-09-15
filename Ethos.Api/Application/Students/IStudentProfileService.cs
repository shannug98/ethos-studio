using Ethos.Api.Contracts.Students;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Students;

public interface IStudentProfileService
{
    Task<StudentProfileResponse> GetMyProfileAsync();

    Task<StudentProfileResponse> UpdateMyProfileAsync(
        UpdateStudentProfileRequest request);

    Task<StudentProfileResponse> UploadProfilePhotoAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType)?> GetMyProfilePhotoStreamAsync(
        CancellationToken cancellationToken = default);
}
