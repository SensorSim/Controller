namespace Controller.Dtos;

public sealed record MeasurementOut(
    string SensorId,
    DateTimeOffset Timestamp,
    double Value
);
