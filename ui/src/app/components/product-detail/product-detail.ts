import { Component, computed, inject, linkedSignal, resource, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { lastValueFrom } from 'rxjs';
import { WebApiService } from '@app/services/web-api-service';
import { CartStore } from '@app/store/cart.store';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.css',
})
export class ProductDetailComponent {
  private readonly webApiService = inject(WebApiService);
  readonly cartStore = inject(CartStore);
  private readonly route = inject(ActivatedRoute);

  readonly productId = signal<string>('');

  constructor() {
    this.route.paramMap.subscribe(params => {
      this.productId.set(params.get('id') ?? '');
    });
  }

  readonly product = resource({
    params: () => ({ id: this.productId() }),
    loader: async ({ params }) => {
      if (!params.id) return undefined;
      return lastValueFrom(this.webApiService.getProduct(params.id));
    },
  });

  readonly thumbnails = computed(() => {
    const images = this.product.value()?.images ?? [];
    return [...images].sort((a, b) => a.sortOrder - b.sortOrder);
  });

  readonly selectedImage = linkedSignal(() => this.thumbnails()[0]?.id ?? '');

  readonly hoverImage = computed(() => {
    const thumbs = this.thumbnails();
    if (thumbs.length <= 1) return '';
    return thumbs.find(t => t.id !== this.selectedImage())?.id ?? '';
  });

  readonly colors = computed(() => this.product.value()?.metadata?.colors ?? []);
  readonly sizes = computed(() => this.product.value()?.metadata?.sizes ?? []);

  readonly selectedColor = linkedSignal(() => this.colors()[0]?.name ?? '');
  readonly selectedSize = linkedSignal(() => this.sizes()[0] ?? '');

  readonly quantity = signal(1);

  readonly hasDiscount = computed(() => !!this.product.value()?.activeOffer?.discountValue);

  readonly discountedPrice = computed(() => {
    const p = this.product.value();
    if (!p?.activeOffer) return p?.price ?? 0;
    return p.activeOffer.discountType === 'FixedAmount'
      ? p.price - p.activeOffer.discountValue
      : Math.round(p.price * (1 - p.activeOffer.discountValue / 100) * 100) / 100;
  });

  selectImage(id: string) {
    this.selectedImage.set(id);
  }

  incrementQty() {
    this.quantity.update(q => q + 1);
  }

  decrementQty() {
    this.quantity.update(q => Math.max(1, q - 1));
  }

  addToCart() {
    const id = this.product.value()?.id;
    if (id) {
      this.cartStore.addToCart(String(id));
    }
  }
}
