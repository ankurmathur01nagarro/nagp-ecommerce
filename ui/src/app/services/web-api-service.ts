import { Injectable, signal } from "@angular/core";
import { environment } from "../../environments/environment";
import { httpResource } from "@angular/common/http";

export interface TokenResponse {
  token: string;
  expiresIn: number;
}

interface Credentials {
  username: string;
  password: string;
}

@Injectable({ providedIn: "root" })
export class WebApiService {
  private readonly baseUrl = `${environment.webApiBaseUrl}/api`;

  private readonly credentials = signal<Credentials | undefined>(undefined);

  readonly token = httpResource<TokenResponse>(() => {
    const creds = this.credentials();
    if (!creds) return undefined; // skip until explicitly triggered
    return {
      url: `${this.baseUrl}/auth/login`,
      method: "POST",
      body: creds,
    };
  });

  login(username: string, password: string) {
    this.credentials.set({ username, password });
  }
}
