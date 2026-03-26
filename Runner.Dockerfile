# Unified Runner for JustBigO-Fun
FROM eclipse-temurin:21-jdk-jammy

# Install Python and G++
RUN apt-get update && apt-get install -y \
    python3 \
    g++ \
    && rm -rf /var/lib/apt/lists/*

# Ensure 'python' command works
RUN ln -s /usr/bin/python3 /usr/bin/python

WORKDIR /app
