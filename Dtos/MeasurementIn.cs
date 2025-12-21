namespace Controller.Dtos;

public sealed record MeasurementIn(
    string SensorId,
    DateTimeOffset Timestamp,
    double Value);
