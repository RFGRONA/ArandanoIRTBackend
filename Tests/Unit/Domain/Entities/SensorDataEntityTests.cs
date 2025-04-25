using ArandanoIRT_Backend.Domain.Entities; 
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Domain.Entities
{
    /// <summary>
    /// Contains unit tests for the <see cref="SensorDataEntity"/> class.
    /// Focuses on constructor validation logic, especially range checks.
    /// </summary>
    public class SensorDataEntityTests
    {
        // --- Default valid values for constructor parameters ---
        /// <summary> Default ID for test sensor data entities. </summary>
        private const int DefaultId = 1;
        /// <summary> Default temperature value for test entities. </summary>
        private const double DefaultTemperature = 25.5;
        /// <summary> A valid humidity percentage for tests (within 0-100 range). </summary>
        private const double ValidHumidity = 50.0;
        /// <summary> A valid light intensity value for tests (non-negative). </summary>
        private const double ValidLightIntensity = 5000;
        /// <summary> Default timestamp for test entities, usually current UTC time. </summary>
        private static readonly DateTime DefaultTimestamp = DateTime.UtcNow;
        /// <summary> Default plant ID for test entities. </summary>
        private const int DefaultPlantId = 10;
        /// <summary> Default crop ID for test entities. </summary>
        private const int DefaultCropId = 20;

        /// <summary>
        /// Creates an instance of <see cref="SensorDataEntity"/> with specified or default valid values,
        /// simplifying test setup.
        /// </summary>
        /// <param name="id">The sensor data record ID.</param>
        /// <param name="temperature">The recorded temperature.</param>
        /// <param name="humidity">The recorded humidity percentage (0-100).</param>
        /// <param name="lightIntensity">The recorded light intensity (non-negative).</param>
        /// <param name="cityTemperature">Optional city temperature at the time of recording.</param>
        /// <param name="cityHumidity">Optional city humidity at the time of recording.</param>
        /// <param name="recordedAt">The timestamp of the recording. Uses default if null.</param>
        /// <param name="plantId">Optional associated Plant ID.</param>
        /// <param name="cropId">Optional associated Crop ID.</param>
        /// <returns>A new instance of <see cref="SensorDataEntity"/>.</returns>
        private static SensorDataEntity CreateSensorDataEntity(
            int id = DefaultId,
            double temperature = DefaultTemperature,
            double humidity = ValidHumidity,
            double lightIntensity = ValidLightIntensity,
            double? cityTemperature = null,
            double? cityHumidity = null,
            DateTime? recordedAt = null,
            int? plantId = DefaultPlantId,
            int? cropId = DefaultCropId)
        {
            // Ensures a valid DateTime is used for RecordedAt.
            return new SensorDataEntity(
                id,
                temperature,
                humidity,
                lightIntensity,
                cityTemperature,
                cityHumidity,
                recordedAt ?? DefaultTimestamp,
                plantId,
                cropId
            );
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentOutOfRangeException when humidity is less than 0.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenHumidityIsLessThanZero()
        {
            // Arrange
            double invalidHumidity = -0.1;

            // Act
            Action act = () => CreateSensorDataEntity(humidity: invalidHumidity);

            // Assert
            // Checks the exception type, message content, and parameter name.
            act.Should().Throw<ArgumentOutOfRangeException>()
               .WithMessage("Humidity must be between 0 and 100. (Parameter 'humidity')")
               .And.ParamName.Should().Be("humidity");
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentOutOfRangeException when humidity is greater than 100.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenHumidityIsGreaterThan100()
        {
            // Arrange
            double invalidHumidity = 100.1;

            // Act
            Action act = () => CreateSensorDataEntity(humidity: invalidHumidity);

            // Assert
            // Checks the exception type, message content, and parameter name.
            act.Should().Throw<ArgumentOutOfRangeException>()
               .WithMessage("Humidity must be between 0 and 100. (Parameter 'humidity')")
               .And.ParamName.Should().Be("humidity");
        }

        /// <summary>
        /// Verifies that the constructor does NOT throw an exception when humidity is exactly 0 (lower boundary).
        /// </summary>
        [Fact]
        public void Constructor_ShouldNotThrow_WhenHumidityIsExactlyZero()
        {
            // Arrange
            double boundaryHumidity = 0.0;

            // Act
            Action act = () => CreateSensorDataEntity(humidity: boundaryHumidity);

            // Assert
            act.Should().NotThrow();
        }

        /// <summary>
        /// Verifies that the constructor does NOT throw an exception when humidity is exactly 100 (upper boundary).
        /// </summary>
        [Fact]
        public void Constructor_ShouldNotThrow_WhenHumidityIsExactly100()
        {
            // Arrange
            double boundaryHumidity = 100.0;

            // Act
            Action act = () => CreateSensorDataEntity(humidity: boundaryHumidity);

            // Assert
            act.Should().NotThrow();
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentOutOfRangeException when light intensity is negative.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenLightIntensityIsNegative()
        {
            // Arrange
            double invalidLightIntensity = -1.0;

            // Act
            Action act = () => CreateSensorDataEntity(lightIntensity: invalidLightIntensity);

            // Assert
            // Checks the exception type, message content, and parameter name.
            act.Should().Throw<ArgumentOutOfRangeException>()
               .WithMessage("Light intensity cannot be negative. (Parameter 'lightIntensity')")
               .And.ParamName.Should().Be("lightIntensity");
        }

        /// <summary>
        /// Verifies that the constructor does NOT throw an exception when light intensity is exactly 0 (lower boundary).
        /// </summary>
        [Fact]
        public void Constructor_ShouldNotThrow_WhenLightIntensityIsExactlyZero()
        {
            // Arrange
            double boundaryLightIntensity = 0.0;

            // Act
            Action act = () => CreateSensorDataEntity(lightIntensity: boundaryLightIntensity);

            // Assert
            act.Should().NotThrow();
        }

        /// <summary>
        /// Verifies that the constructor successfully creates an instance and assigns properties
        /// correctly when all arguments are valid.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenValidArgumentsProvided()
        {
            // Arrange
            int expectedId = 5;
            double expectedTemperature = 22.3;
            double expectedHumidity = 65.7;
            double expectedLightIntensity = 7800.5;
            double? expectedCityTemperature = 28.1;
            double? expectedCityHumidity = 70.2;
            DateTime expectedRecordedAt = DateTime.UtcNow.AddHours(-1);
            int? expectedPlantId = 15;
            int? expectedCropId = 25;

            // Act
            var sensorData = new SensorDataEntity(
                expectedId,
                expectedTemperature,
                expectedHumidity,
                expectedLightIntensity,
                expectedCityTemperature,
                expectedCityHumidity,
                expectedRecordedAt,
                expectedPlantId,
                expectedCropId
            );

            // Assert
            sensorData.IdSensorData.Should().Be(expectedId);
            sensorData.Temperature.Should().Be(expectedTemperature);
            sensorData.Humidity.Should().Be(expectedHumidity);
            sensorData.LightIntensity.Should().Be(expectedLightIntensity);
            sensorData.CityTemperature.Should().Be(expectedCityTemperature);
            sensorData.CityHumidity.Should().Be(expectedCityHumidity);
            sensorData.RecordedAt.Should().Be(expectedRecordedAt);
            sensorData.PlantId.Should().Be(expectedPlantId);
            sensorData.CropId.Should().Be(expectedCropId);
        }

        /// <summary>
        /// Verifies that the constructor works correctly when optional arguments
        /// (CityTemperature, CityHumidity, PlantId, CropId) are null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenOptionalValuesAreNull()
        {
            // Arrange
            int expectedId = 6;
            double expectedTemperature = 19.0;
            double expectedHumidity = 40.0;
            double expectedLightIntensity = 1000;
            DateTime expectedRecordedAt = DateTime.UtcNow.AddDays(-2);
            // Explicitly setting optional values to null.
            double? nullCityTemp = null;
            double? nullCityHumidity = null;
            int? nullPlantId = null;
            int? nullCropId = null;

            // Act
            var sensorData = new SensorDataEntity(
               expectedId,
               expectedTemperature,
               expectedHumidity,
               expectedLightIntensity,
               nullCityTemp,
               nullCityHumidity,
               expectedRecordedAt,
               nullPlantId,
               nullCropId
            );

            // Assert
            sensorData.IdSensorData.Should().Be(expectedId);
            sensorData.Temperature.Should().Be(expectedTemperature);
            sensorData.Humidity.Should().Be(expectedHumidity);
            sensorData.LightIntensity.Should().Be(expectedLightIntensity);
            sensorData.RecordedAt.Should().Be(expectedRecordedAt);
            // Verifies that nullable properties are correctly assigned as null.
            sensorData.CityTemperature.Should().BeNull();
            sensorData.CityHumidity.Should().BeNull();
            sensorData.PlantId.Should().BeNull();
            sensorData.CropId.Should().BeNull();
        }
    }
}