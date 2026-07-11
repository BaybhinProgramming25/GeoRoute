package com.georoute.model;

import java.util.List;

public record RouteResult(List<Coordinate> geometry, List<RouteStep> steps, double totalDistance, double totalDuration, boolean fromCache) {}
