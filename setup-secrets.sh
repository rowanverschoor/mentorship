#!/usr/bin/env bash
# Run once per PC: secrets for Docker (.env) and dotnet run (user-secrets).
set -e
cd "$(dirname "$0")"
read -rp "API Ninjas key: " KEY
JWT=$(openssl rand -base64 48)

cat > .env <<EOF
API_NINJAS_KEY=$KEY
JWT_KEY=$JWT
AUTH_USER=rowan
AUTH_PASSWORD=test123
EOF

cd backend/WeatherApi
dotnet user-secrets set ApiNinjas:Key "$KEY"
dotnet user-secrets set Jwt:Key "$JWT"
dotnet user-secrets set Auth:User rowan
dotnet user-secrets set Auth:Password test123