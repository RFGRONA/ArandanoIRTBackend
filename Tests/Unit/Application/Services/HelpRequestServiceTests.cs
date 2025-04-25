using ArandanoIRT_Backend.Application.DTOs.Auth; 
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Application.Services;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Application.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="HelpRequestService"/> class.
    /// </summary>
    public class HelpRequestServiceTests : IDisposable
    {
        // Mocks
        /// <summary> Mock for the crop repository dependency. </summary>
        private readonly Mock<ICropRepository> _mockCropRepository;
        /// <summary> Mock for the person repository dependency. </summary>
        private readonly Mock<IPersonRepository> _mockPersonRepository;
        /// <summary> Mock for the email service dependency. </summary>
        private readonly Mock<IEmailService> _mockEmailService;
        /// <summary> Mock for the logger dependency. </summary>
        private readonly Mock<ILogger<HelpRequestService>> _mockLogger;

        // SUT (System Under Test)
        /// <summary> The instance of the service being tested. </summary>
        private readonly HelpRequestService _helpRequestService;

        // Common data for tests
        /// <summary> The name of the test crop. </summary>
        private readonly string _cropName = "Test Crop";
        /// <summary> The ID of the test crop. </summary>
        private readonly int _cropId = 1;
        /// <summary> The ID of the test administrator user. </summary>
        private readonly int _adminId = 10;
        /// <summary> The email address of the test administrator user. </summary>
        private readonly string _adminEmail = "admin@crop.com";
        /// <summary> A common PersonEntity instance representing an admin. </summary>
        private readonly PersonEntity _adminPerson;
        /// <summary> A common CropEntity instance linked to the admin. </summary>
        private readonly CropEntity _cropEntity;
        /// <summary> A valid help request DTO used in tests. </summary>
        private readonly HelpRequestDto _validHelpRequest;

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpRequestServiceTests"/> class,
        /// setting up all mock dependencies and the service under test.
        /// </summary>
        public HelpRequestServiceTests()
        {
            _mockCropRepository = new Mock<ICropRepository>();
            _mockPersonRepository = new Mock<IPersonRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockLogger = new Mock<ILogger<HelpRequestService>>();

            // Setup common entity instances used across tests.
            _adminPerson = new PersonEntity(
                idPerson: _adminId, firstName: "Admin", lastName: "Crop", email: _adminEmail, password: "hash",
                createdAt: DateTime.UtcNow.AddDays(-5), isAdmin: true, allNotifications: true, cropId: _cropId);

            // Creates a crop entity linked to the admin user.
            // Assumes this constructor signature for CropEntity; adjust if necessary.
            _cropEntity = new CropEntity(
                 idCrop: _cropId, nameCrop: _cropName, addressCrop: "Addr", cityName: "City",
                 createdAt: DateTime.UtcNow.AddDays(-5), adminUserId: _adminId);

            // Initializes a valid help request DTO.
            _validHelpRequest = new HelpRequestDto
            {
                Name = "Help Seeker",
                Email = "seeker@example.com",
                Subject = "Need Help",
                CropName = _cropName,
                Message = "My plants look sad.",
                CaptchaToken = null // Captcha handling not tested here.
            };

            // Instantiate the System Under Test (SUT) with mock objects.
            _helpRequestService = new HelpRequestService(
                _mockCropRepository.Object,
                _mockPersonRepository.Object,
                _mockEmailService.Object,
                _mockLogger.Object
            );
        }

        /// <summary>
        /// Disposes resources used by the test context, if any.
        /// </summary>
        public void Dispose()
        {
            // No explicit disposable resources in this setup.
            GC.SuppressFinalize(this);
        }

        #region SendUnauthenticatedHelpAsync Tests

        [Fact]
        public async Task SendUnauthenticatedHelpAsync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = _validHelpRequest;
            var emailBody = "<p>Help needed!</p>";

            // Setup: Crop repository successfully finds the crop by name.
            // Returns Task<Result<CropEntity>>.
            _mockCropRepository.Setup(repo => repo.GetByNameAsync(request.CropName))
                             .ReturnsAsync(Result<CropEntity>.Success(_cropEntity));
            // Setup: Person repository successfully finds the admin by ID.
            // Returns Task<Result<PersonEntity>>.
            _mockPersonRepository.Setup(repo => repo.GetById(_adminId))
                                 .ReturnsAsync(Result<PersonEntity>.Success(_adminPerson));
            // Setup: Email service successfully generates the email body.
            // Returns string.
            _mockEmailService.Setup(svc => svc.GenerateHelpRequestBody(request))
                             .Returns(emailBody);
            // Setup: Email service successfully sends the email.
            // Returns Task<Result>.
            _mockEmailService.Setup(svc => svc.SendEmailAsync(_adminEmail, It.IsAny<string>(), emailBody))
                             .Returns(Task.FromResult(Result.Success()));

            // Act
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.ErrorMessage.Should().BeNullOrEmpty();

            // Verify dependencies were called with expected arguments.
            _mockCropRepository.Verify(repo => repo.GetByNameAsync(request.CropName), Times.Once);
            _mockPersonRepository.Verify(repo => repo.GetById(_adminId), Times.Once);
            _mockEmailService.Verify(svc => svc.GenerateHelpRequestBody(request), Times.Once);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(_adminEmail, It.Is<string>(s => s.Contains(request.Subject)), emailBody), Times.Once);
        }

        [Fact]
        public async Task SendUnauthenticatedHelpAsync_NullRequest_ReturnsFailure()
        {
            // Arrange
            HelpRequestDto? request = null;

            // Act
            // Uses null-forgiving operator for the test scenario.
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request!);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the expected service error message for null input.
            result.ErrorMessage.Should().Be("Help request data cannot be null.");
        }

        [Theory]
        [InlineData("", "email@a.com", "Sub", "Crop", "Msg")] // Scenario: Empty Name
        [InlineData("Name", "", "Sub", "Crop", "Msg")]       // Scenario: Empty Email
        [InlineData("Name", "email@a.com", "", "Crop", "Msg")] // Scenario: Empty Subject
        [InlineData("Name", "email@a.com", "Sub", "", "Msg")]   // Scenario: Empty CropName
        [InlineData("Name", "email@a.com", "Sub", "Crop", "")]  // Scenario: Empty Message
        public async Task SendUnauthenticatedHelpAsync_MissingRequiredFields_ReturnsFailure(
            string name, string email, string subject, string cropName, string message)
        {
            // Arrange
            var request = new HelpRequestDto
            {
                Name = name,
                Email = email,
                Subject = subject,
                CropName = cropName,
                Message = message
            };

            // Act
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the expected service validation error message.
            result.ErrorMessage.Should().Be("All fields (Name, Email, Subject, Crop Name, Message) are required.");

            // Verify dependencies were not called due to early validation failure.
            _mockCropRepository.Verify(repo => repo.GetByNameAsync(It.IsAny<string>()), Times.Never);
            _mockPersonRepository.Verify(repo => repo.GetById(It.IsAny<int>()), Times.Never);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendUnauthenticatedHelpAsync_CropNotFound_ReturnsFailure()
        {
            // Arrange
            var request = _validHelpRequest;
            var cropError = "Crop DB error";

            // Setup: Crop repository fails to find the crop.
            // Returns Task<Result<CropEntity>> Failure.
            _mockCropRepository.Setup(repo => repo.GetByNameAsync(request.CropName))
                             .ReturnsAsync(Result<CropEntity>.Failure(cropError));

            // Act
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the specific user-facing error message from the service.
            result.ErrorMessage.Should().Be($"The specified crop '{request.CropName}' could not be found.");

            // Verify dependencies were called appropriately.
            _mockCropRepository.Verify(repo => repo.GetByNameAsync(request.CropName), Times.Once);
            _mockPersonRepository.Verify(repo => repo.GetById(It.IsAny<int>()), Times.Never); // Should fail before admin lookup
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendUnauthenticatedHelpAsync_CropHasNoAdmin_ReturnsFailure()
        {
            // Arrange
            var request = _validHelpRequest;
            // Creates a crop entity instance without an assigned AdminUserId.
            var cropWithoutAdmin = new CropEntity(
                 idCrop: _cropId, nameCrop: _cropName, addressCrop: "Addr", cityName: "City",
                 createdAt: DateTime.UtcNow.AddDays(-5), adminUserId: null); // AdminUserId is null

            // Setup: Crop repository finds the crop (without an admin).
            _mockCropRepository.Setup(repo => repo.GetByNameAsync(request.CropName))
                             .ReturnsAsync(Result<CropEntity>.Success(cropWithoutAdmin));

            // Act
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the specific user-facing error message from the service.
            result.ErrorMessage.Should().Be("Could not process the request because the crop does not have an assigned administrator.");

            // Verify dependencies were called appropriately.
            _mockCropRepository.Verify(repo => repo.GetByNameAsync(request.CropName), Times.Once);
            // Verifies GetById is not called because the crop has no admin ID.
            _mockPersonRepository.Verify(repo => repo.GetById(It.IsAny<int>()), Times.Never);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }


        [Fact]
        public async Task SendUnauthenticatedHelpAsync_AdminUserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = _validHelpRequest;
            var adminError = "Admin user DB error";

            // Setup: Crop repository finds the crop successfully.
            _mockCropRepository.Setup(repo => repo.GetByNameAsync(request.CropName))
                             .ReturnsAsync(Result<CropEntity>.Success(_cropEntity));
            // Setup: Person repository fails to find the admin user by ID.
            // Returns Task<Result<PersonEntity>> Failure.
            _mockPersonRepository.Setup(repo => repo.GetById(_adminId))
                                 .ReturnsAsync(Result<PersonEntity>.Failure(adminError));

            // Act
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the specific user-facing error message from the service.
            result.ErrorMessage.Should().Be("Could not process the request due to an internal error finding the administrator.");

            // Verify dependencies were called appropriately.
            _mockCropRepository.Verify(repo => repo.GetByNameAsync(request.CropName), Times.Once);
            _mockPersonRepository.Verify(repo => repo.GetById(_adminId), Times.Once);
            // Verifies failure occurs before generating email body.
            _mockEmailService.Verify(svc => svc.GenerateHelpRequestBody(It.IsAny<HelpRequestDto>()), Times.Never);
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendUnauthenticatedHelpAsync_EmailBodyGenerationFails_ReturnsFailure()
        {
            // Arrange
            var request = _validHelpRequest;
            var generationException = new InvalidOperationException("Template error");

            // Setup: Dependencies succeed up to the point of email body generation.
            _mockCropRepository.Setup(repo => repo.GetByNameAsync(request.CropName))
                             .ReturnsAsync(Result<CropEntity>.Success(_cropEntity));
            _mockPersonRepository.Setup(repo => repo.GetById(_adminId))
                                 .ReturnsAsync(Result<PersonEntity>.Success(_adminPerson));
            // Setup: Email service fails during body generation.
            // Configures mock to throw an exception.
            _mockEmailService.Setup(svc => svc.GenerateHelpRequestBody(request))
                             .Throws(generationException);

            // Act
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the expected service error message for this failure scenario.
            result.ErrorMessage.Should().Be("Failed to prepare the help request email.");

            // Verify dependencies were called up to the point of failure.
            _mockCropRepository.Verify(repo => repo.GetByNameAsync(request.CropName), Times.Once);
            _mockPersonRepository.Verify(repo => repo.GetById(_adminId), Times.Once);
            _mockEmailService.Verify(svc => svc.GenerateHelpRequestBody(request), Times.Once);
            // Verifies email sending does not happen.
            _mockEmailService.Verify(svc => svc.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendUnauthenticatedHelpAsync_EmailSendingFails_ReturnsFailure()
        {
            // Arrange
            var request = _validHelpRequest;
            var emailBody = "<p>Help needed!</p>";
            var emailError = "SMTP server down";

            // Setup: Dependencies succeed up to the point of email sending.
            _mockCropRepository.Setup(repo => repo.GetByNameAsync(request.CropName))
                             .ReturnsAsync(Result<CropEntity>.Success(_cropEntity));
            _mockPersonRepository.Setup(repo => repo.GetById(_adminId))
                                 .ReturnsAsync(Result<PersonEntity>.Success(_adminPerson));
            _mockEmailService.Setup(svc => svc.GenerateHelpRequestBody(request))
                             .Returns(emailBody);
            // Setup: Email service fails during sending.
            // Returns Task<Result> Failure.
            _mockEmailService.Setup(svc => svc.SendEmailAsync(_adminEmail, It.IsAny<string>(), emailBody))
                             .Returns(Task.FromResult(Result.Failure(emailError)));

            // Act
            var result = await _helpRequestService.SendUnauthenticatedHelpAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Matches the service error message, which might include the original error.
            result.ErrorMessage.Should().Contain("Failed to send the help request email.");
            result.ErrorMessage.Should().Contain(emailError);

            // Verify dependencies were called, including the failed send attempt.
            _mockCropRepository.Verify(repo => repo.GetByNameAsync(request.CropName), Times.Once);
            _mockPersonRepository.Verify(repo => repo.GetById(_adminId), Times.Once);
            _mockEmailService.Verify(svc => svc.GenerateHelpRequestBody(request), Times.Once);
            // Verifies email sending was attempted.
            _mockEmailService.Verify(svc => svc.SendEmailAsync(_adminEmail, It.Is<string>(s => s.Contains(request.Subject)), emailBody), Times.Once);
        }

        #endregion
    }
}