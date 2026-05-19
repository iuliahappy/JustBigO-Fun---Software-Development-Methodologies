# Unified Runner for JustBigO-Fun
FROM eclipse-temurin:21-jdk-jammy

# Install Python, G++, and utilities for C++ JSON
RUN apt-get update && apt-get install -y \
    python3 \
    g++ \
    curl \
    ca-certificates \
    time \
    && rm -rf /var/lib/apt/lists/*

# Pre-download nlohmann/json for C++
RUN mkdir -p /usr/include/nlohmann && \
    curl -L https://github.com/nlohmann/json/releases/download/v3.11.3/json.hpp -o /usr/include/nlohmann/json.hpp

# Pre-download Gson for Java
RUN mkdir -p /usr/share/java && \
    curl -L https://repo1.maven.org/maven2/com/google/code/gson/gson/2.10.1/gson-2.10.1.jar -o /usr/share/java/gson.jar

# Ensure 'python' command works
RUN ln -s /usr/bin/python3 /usr/bin/python

WORKDIR /app
