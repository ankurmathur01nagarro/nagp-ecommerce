import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export type CartDetails = {
  items: {
    productId: string;
    quantity: number;
    price: number;
    offer?: {
      name: string;
      discountType: 'FixedAmount' | 'Percentage';
      discountValue: number;
      endsAt: Date;
    };
  }[];
  totalPrice: number;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.webApiBaseUrl}/api`;

  addToCart(productId: string): Observable<CartDetails> {
    return this.http.post<CartDetails> (`${this.baseUrl}/cart`, {
      productId,
    });
  }

  removeFromCart(productId: string): Observable<CartDetails> {
    return this.http.delete<CartDetails>(`${this.baseUrl}/cart/items/${productId}`);
  }

  clearCart(): Observable<CartDetails> {
    return this.http.delete<CartDetails>(`${this.baseUrl}/cart`);
  }
}
