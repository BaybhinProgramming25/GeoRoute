package com.georoute.controller;

import com.fasterxml.jackson.databind.ObjectMapper;
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
import java.util.Map;

@RestController
@RequestMapping("/api")
public class RouteController {

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
        String cacheKey = "route:%s,%s:%s,%s".formatted(
                req.source().lat(), req.source().lon(),
                req.destination().lat(), req.destination().lon());
        String lockKey = "lock:" + cacheKey;

        String cached = redis.opsForValue().get(cacheKey);
        if (cached != null)
            return ResponseEntity.ok(mapper.readValue(cached, RouteResult.class));

        Boolean acquired = redis.opsForValue().setIfAbsent(lockKey, "1", Duration.ofSeconds(30));
        if (!Boolean.TRUE.equals(acquired)) {
            for (int i = 0; i < 60; i++) {
                Thread.sleep(500);
                cached = redis.opsForValue().get(cacheKey);
                if (cached != null)
                    return ResponseEntity.ok(mapper.readValue(cached, RouteResult.class));
            }
            return ResponseEntity.status(503).body(Map.of("message", "Route computation timed out, please retry."));
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
}
