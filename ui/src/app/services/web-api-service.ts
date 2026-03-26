import { Injectable, signal } from "@angular/core";
import { environment } from "../../environments/environment";
import { httpResource } from "@angular/common/http";

export interface TokenResponse {
  token: string;
  expiresIn: number;
}

@Injectable({ providedIn: "root" })
export class WebApiService {
  private readonly baseUrl = environment.webApiBaseUrl;

  constructor() {}

  readonly username = signal("");
  readonly password = signal("");

  readonly token = httpResource<TokenResponse>(() => ({
    url: `${this.baseUrl}/login`,
    method: "POST",
    body: {
      username: this.username(),
      password: this.password()
    }
  }));
}
