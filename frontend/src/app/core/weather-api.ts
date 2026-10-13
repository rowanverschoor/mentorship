import { Observable } from 'rxjs';
import { Weather } from '../models/weather';

// The contract the UI depends on. Components inject this, never the HTTP class,
// so the data source can be swapped (real API, mock, cache) without touching them.
export abstract class WeatherApi {
  abstract get(latitude: number, longitude: number): Observable<Weather>;
}
