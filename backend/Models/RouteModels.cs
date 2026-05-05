namespace EZTravel.Models;

public record RouteRequest(Coordinate Source, Coordinate Destination, string Mode);
public record Coordinate(double Lat, double Lon);
public record RouteStep(string Instruction, double Distance, int[] WayPoints);
public record RouteResult(List<Coordinate> Geometry, List<RouteStep> Steps, double TotalDistance, double TotalDuration, bool FromCache = false);
