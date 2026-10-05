// Shape of the JSON returned by our backend (/weather).
// Field names must match the backend's WeatherResult, in camelCase.
export interface Weather {
  temperature: number;
  feelsLike: number;
  humidity: number;
  windSpeed: number;
  cloudPercentage: number;
}