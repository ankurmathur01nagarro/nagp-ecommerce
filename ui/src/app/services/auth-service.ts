import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { SKIP_AUTH } from './authInterceptor';

export type TokenResponse = {
  username: string;
  expiresAt: Date;
  token: string;
};

type Credentials = {
  username: string;
  password: string;
};

type RegisterRequest = Credentials & {
  email: string;
};

export type UserInfo = {
  sub: string;
  name: string;
  email: string;
  emailVerified: boolean;
  phoneNumber?: string;
  role: string;
};

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.webApiBaseUrl}/api`;

  login(credentials: Credentials): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.baseUrl}/auth/login`, credentials, {
      context: new HttpContext().set(SKIP_AUTH, true)
    });
  }

  register(req: RegisterRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/auth/register`, req, {
      context: new HttpContext().set(SKIP_AUTH, true)
    });
  }

  userInfo(): Observable<UserInfo> {
    return this.http.get<UserInfo>(`${this.baseUrl}/auth/me`);
  }
}
