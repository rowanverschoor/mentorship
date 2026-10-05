#!/usr/bin/env bash
# Starts backend and frontend together; Ctrl+C stops both.
cd "$(dirname "$0")"

trap 'kill 0' EXIT

(cd backend/WeatherApi && dotnet run --launch-profile http) &
(cd frontend && npm start) &

wait
