import { inject, Injectable } from "@angular/core";
import { environment } from "@env/environment";
import { HttpClient } from "@angular/common/http";

@Injectable({ providedIn: "root" })
export class WebApiService {
  private readonly baseUrl = `${environment.webApiBaseUrl}/api`;
  readonly http = inject(HttpClient);
}
