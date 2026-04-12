import { inject, Injectable } from "@angular/core";
import { environment } from "@env/environment";
import { HttpClient } from "@angular/common/http";

export type Category = {
  id: string;
  name: string;
  subcategories: Category[];
}

@Injectable({ providedIn: "root" })
export class WebApiService {
  private readonly baseUrl = `${environment.webApiBaseUrl}/api`;
  readonly http = inject(HttpClient);

  constructor() {}

  getCategories() {
    return this.http.get<Category[]>(`${this.baseUrl}/categories`);
  }
}
