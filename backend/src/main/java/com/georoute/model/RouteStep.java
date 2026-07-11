package com.georoute.model;

public record RouteStep(String instruction, double distance, int[] wayPoints) {}
