import { Observable } from 'rxjs';

// The contract for exchanging credentials for a token. Auth depends on this,
// never on HTTP, so the login mechanism can be swapped or mocked.
export abstract class AuthApi {
  abstract login(username: string, password: string): Observable<string>;
}
