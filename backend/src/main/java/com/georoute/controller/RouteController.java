package com.georoute.controller;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.georoute.model.Coordinate;
import com.georoute.model.RouteRequest;
import com.georoute.model.RouteResult;
import com.georoute.service.RoutingService;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.Duration;
import java.util.List;
import java.util.Map;

@RestController
@RequestMapping("/api")
public class RouteController {

    // Rough bounding box around the NYC metro area covered by the imported OSM extract
    private static final double MIN_LAT = 40.45, MAX_LAT = 41.0;
    private static final double MIN_LON = -74.30, MAX_LON = -73.65;

    private final StringRedisTemplate redis;
    private final RoutingService routingService;
    private final ObjectMapper mapper;

    public RouteController(StringRedisTemplate redis, RoutingService routingService, ObjectMapper mapper) {
        this.redis = redis;
        this.routingService = routingService;
        this.mapper = mapper;
    }

    @PostMapping("/route")
    public ResponseEntity<?> getRoute(@RequestBody RouteRequest req) throws Exception {
        
        String validationError = validate(req);
        if (validationError != null)
            return ResponseEntity.badRequest().body(Map.of("message", validationError));

        String cacheKey = "route:%s,%s:%s,%s".formatted(
                req.source().lat(), req.source().lon(),
                req.destination().lat(), req.destination().lon());
        String lockKey = "lock:" + cacheKey;

        String cached = redis.opsForValue().get(cacheKey);
        if (cached != null)
            return ResponseEntity.ok(mapper.readValue(cached, RouteResult.class));

        Boolean acquired = redis.opsForValue().setIfAbsent(lockKey, "1", Duration.ofSeconds(30));
        if (!Boolean.TRUE.equals(acquired)) {
            for (int i = 0; i < 6; i++) {
                Thread.sleep(500);
                cached = redis.opsForValue().get(cacheKey);
                if (cached != null)
                    return ResponseEntity.ok(mapper.readValue(cached, RouteResult.class));
            }
            return ResponseEntity.status(503)
                    .header("Retry-After", "3")
                    .body(Map.of("message", "This route is still being computed, please retry shortly."));
        }

        try {
            RouteResult result = routingService.getRoute(req.source(), req.destination());
            redis.opsForValue().set(cacheKey, mapper.writeValueAsString(result), Duration.ofHours(1));
            return ResponseEntity.ok(result);
        } catch (Exception ex) {
            ex.printStackTrace();
            return ResponseEntity.status(500).body(Map.of("message", "Route calculation failed"));
        } finally {
            redis.delete(lockKey);
        }
    }

    private static String validate(RouteRequest req) {
        if (req == null || req.source() == null || req.destination() == null)
            return "Both source and destination coordinates are required.";
        for (Coordinate c : List.of(req.source(), req.destination())) {
            if (!Double.isFinite(c.lat()) || !Double.isFinite(c.lon()))
                return "Coordinates must be finite numbers.";
            if (c.lat() < MIN_LAT || c.lat() > MAX_LAT || c.lon() < MIN_LON || c.lon() > MAX_LON)
                return "Coordinates must be within the New York City area.";
        }
        return null;
    }
}
