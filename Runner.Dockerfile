# Unified Runner for JustBigO-Fun
FROM eclipse-temurin:21-jdk-jammy

# Install Python, G++, and utilities for C++ JSON
RUN apt-get update && apt-get install -y \
    python3 \
    g++ \
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Pre-download nlohmann/json for C++
RUN mkdir -p /usr/local/include/nlohmann && \
    curl -L https://github.com/nlohmann/json/releases/download/v3.11.3/json.hpp -o /usr/local/include/nlohmann/json.hpp

# Ensure 'python' command works
RUN ln -s /usr/bin/python3 /usr/bin/python

WORKDIR /app
