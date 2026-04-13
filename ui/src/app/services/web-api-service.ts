import { inject, Injectable, type Signal } from "@angular/core";
import { environment } from "@env/environment";
import { HttpClient } from "@angular/common/http";

export type Category = {
  id: string;
  name: string;
  parentCategoryId: number | null;
  subcategories: Category[];
}

export type ProductSearchRequest = {
  page: number;
  pageSize: number;
  colors?: string[];
  inStock?: boolean;
  sizes?: string[];
  priceMin?: number;
  priceMax?: number;
  brands?: string[];
  tags?: string[];
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  category?: string;
  gender?: string;
  ratingMin?: number;
  ratingMax?: number;
}

export type CategoryFacet = {
  categoryId: number;
  categoryName: string;
  parentCategoryId: number | null;
  count: number;
}

export type FacetCount = {
  value: string;
  count: number;
}

export type ColorFacet = {
  name: string;
  hexCode: string | null;
  count: number;
}

export type ProductFacets = {
  categories: CategoryFacet[];
  colors: ColorFacet[];
  sizes: FacetCount[];
  brands: FacetCount[];
  tags: FacetCount[];
}

export type ProductSearchResponse = {
  items: Product[];
  totalCount: number;
  page: number;
  pageSize: number;
  facets: ProductFacets;
}

export type Product = {
  id: string;
  name: string;
  sku: string;
  shortDescription?: string;
  description?: string;
  price: number;
  categoryId: number;
  categoryName?: string;
  brandId: number;
  brandName?: string;
  gender?: string;
  images: {
    id: string;
    url: string;
    alt?: string;
    sortOrder: number;
  }[];
  metadata: {
    colors?: {
      name: string;
      hexCode: string;
    }[];
    sizes?: string[];
    tags?: string[];
    techSpecs?: { label: string; value: string }[];
    rating?: number;
    additionalInfo?: string;
  };
  availableQuantity: number;
  inStock: boolean;
  activeOffer?: {
    name: string;
    discountType: 'FixedAmount' | 'Percentage';
    discountValue: number;
    endsAt: Date;
  },
  createdAt?: Date;
  updatedAt?: Date;
}

@Injectable({ providedIn: "root" })
export class WebApiService {
  private readonly baseUrl = `${environment.webApiBaseUrl}/api`;
  readonly http = inject(HttpClient);

  constructor() {}

  getCategories() {
    return this.http.get<Category[]>(`${this.baseUrl}/categories`);
  }

  getProduct(id: string) {
    return this.http.get<Product>(`${this.baseUrl}/products/${id}`);
  }

  searchProducts(request: ProductSearchRequest) {
    return this.http.post<ProductSearchResponse>(
      `${this.baseUrl}/products/search`,
      request
    );
  }
}
