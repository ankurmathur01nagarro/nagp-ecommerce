import { Component, computed, effect, inject, signal } from '@angular/core';
import { form, FormField } from "@angular/forms/signals";
import { WebApiService, type Category, type Product, type ProductFacets, type ProductSearchRequest } from '@app/services/web-api-service';
import { CartStore } from '@app/store/cart.store';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PriceRangeSliderDirective } from './price-range-slider.directive';

type CatalogFilters = {
  category?: string;
  colors?: string[];
  brands?: string[];
  sizes?: string[];
  tags?: string[];
  availability?: string;
  rating: { min?: number; max?: number };
  priceRange: { min: number; max: number }
  pagination: {
    page: number;
    pageSize: string;
  };
  sortBy: string;
  inStock: string;
};

type ProductVM = Product & {
  isNew: boolean;
  hasDiscount: boolean;
  discountedPrice?: number;
}

@Component({
  selector: 'app-catalog-component',
  imports: [FormField, PriceRangeSliderDirective, RouterLink],
  templateUrl: './catalog-component.html',
  styleUrl: './catalog-component.css',
})
export class CatalogComponent {
  readonly webApiService = inject(WebApiService);
  readonly cartStore = inject(CartStore);
  readonly route = inject(ActivatedRoute);

  sortByOptions = [
    { value: 'name', display: 'Name' },
    { value: 'priceAsc', display: 'Price: Low to High' },
    { value: 'priceDesc', display: 'Price: High to Low' },
    { value: 'newest', display: 'Newest Arrivals' },
    { value: 'rating', display: 'Customer Rating' },
  ];

  paginationOptions = ['10', '15', '20'];

  inStockOptions = [
    { value: '', display: 'All' },
    { value: 'inStock', display: 'In Stock' },
    { value: 'outOfStock', display: 'Out of Stock' },
  ];

  readonly productsList = signal<Product[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly facets = signal<ProductFacets>({
    categories: [],
    colors: [],
    sizes: [],
    brands: [],
    tags: [],
  });

  readonly totalItems = signal(0);
  readonly totalPages = signal(1);

  readonly products = computed<ProductVM[]>(() => {
    return this.productsList().map(item => {
      const finalPrice = item.activeOffer?.discountType == 'FixedAmount' ?
        item.price - item.activeOffer.discountValue :
        Math.round(item.price * (1 - (item.activeOffer?.discountValue ?? 0) / 100) * 100) / 100;
      return {
        ...item,
        isNew: item.updatedAt ? (new Date().getTime() - new Date(item.updatedAt).getTime()) < 30 * 24 * 60 * 60 * 1000 : false,
        hasDiscount: !!item.activeOffer?.discountValue,
        discountedPrice: finalPrice,
      };
    });
  });

  readonly model = signal<CatalogFilters>({
    priceRange: { min: 0, max: 1000 },
    rating: {},
    pagination: {
      page: 1,
      pageSize: '15',
    },
    sortBy: 'name',
    inStock: '',
  });

  readonly pages = computed(() => {
    return Array.from({ length: this.totalPages() }, (_, i) => i + 1);
  });

  readonly itemStart = computed(() => {
    const pageSize = parseInt(this.model().pagination?.pageSize ?? '15');
    return ((this.model().pagination?.page ?? 1) - 1) * pageSize + 1;
  });

  readonly itemEnd = computed(() => {
    const pageSize = parseInt(this.model().pagination?.pageSize ?? '15');
    return Math.min((this.model().pagination?.page ?? 1) * pageSize, this.totalItems());
  });

  readonly searchRequest = computed<ProductSearchRequest>(() => {
    const { sortBy, sortDir } = this.resolveSortParams(this.model().sortBy);
    return {
      page: this.model().pagination?.page ?? 1,
      pageSize: parseInt(this.model().pagination?.pageSize ?? '15'),
      sortBy,
      sortDir,
      inStock: this.model().inStock === 'inStock' ? true : this.model().inStock === 'outOfStock' ? false : undefined,
      category: this.model().category || undefined,
      brands: this.model().brands ? this.model().brands : undefined,
      colors: this.model().colors ? this.model().colors : undefined,
      sizes: this.model().sizes ? this.model().sizes : undefined,
      tags: this.model().tags ? this.model().tags : undefined,
      priceMax: this.model().priceRange.max,
      priceMin: this.model().priceRange.min,
      ratingMin: this.model().rating?.min,
      ratingMax: this.model().rating?.max,
    };
  }, { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) });

  private resolveSortParams(sortBy: string): { sortBy: string; sortDir: 'asc' | 'desc' } {
    switch (sortBy) {
      case 'priceAsc':  return { sortBy: 'price',  sortDir: 'asc' };
      case 'priceDesc': return { sortBy: 'price',  sortDir: 'desc' };
      case 'rating':    return { sortBy: 'rating', sortDir: 'desc' };
      case 'newest':    return { sortBy: 'name',   sortDir: 'desc' };
      default:          return { sortBy: 'name',   sortDir: 'asc' };
    }
  }

  readonly modelForm = form(this.model);

  constructor() {
    const catId = this.route.snapshot.paramMap.get('catId');
    if (catId) {
      this.model.update(m => ({
        ...m,
        category: catId ? catId : undefined,
      }));
    }

    effect(() => {
      this.webApiService.getCategories()
        .subscribe(categories => {
          this.categories.set(categories);
        });
    });

    effect(() => {
      this.webApiService.searchProducts(this.searchRequest())
        .subscribe(response => {
          this.productsList.set(response.items);
          this.facets.set(response.facets);
          this.totalItems.set(response.totalCount);
          const strPageSize = response.pageSize.toString();
          this.model.update(m => ({
            ...m,
            pagination: {
              ...m.pagination,
              pageSize: strPageSize
            }
          }));
          this.totalPages.set(Math.ceil(response.totalCount / +response.pageSize));
        });
    });
  }

  getCategoryProductCount(categoryId: string): number {
    const categoryFacet = this.facets().categories.find(c => c.categoryId === +categoryId);
    const categoryCount = categoryFacet ? categoryFacet.count : 0;

    const subCategories = this.categories()
      .flatMap(c => c.subcategories)
      .find(c => c.id === categoryId)
      ?.subcategories ?? [];
    const subCategoryCount = subCategories.reduce((acc, c) => {
      const count = this.getCategoryProductCount(c.id);
      acc += count;
      return acc;
    }, 0);

    return categoryCount + subCategoryCount;
  }

  setPage(page: number) {
    this.model.set({
      ...this.model(),
      pagination: {
        ...this.model().pagination,
        page
      }
    });
  }

  async addToCart(productId: string) {
    await this.cartStore.addToCart(productId);
  }

  toggleBrand(brand: string) {
    const current = this.model().brands ?? [];
    const updated = current.includes(brand)
      ? current.filter(b => b !== brand)
      : [...current, brand];
    this.model.update(m => ({ ...m, brands: updated.length ? updated : undefined }));
  }

  toggleTag(tag: string) {
    const current = this.model().tags ?? [];
    const updated = current.includes(tag)
      ? current.filter(t => t !== tag)
      : [...current, tag];
    this.model.update(m => ({ ...m, tags: updated.length ? updated : undefined }));
  }

  toggleColor(color: string) {
    const current = this.model().colors ?? [];
    const updated = current.includes(color)
      ? current.filter(c => c !== color)
      : [...current, color];
    this.model.update(m => ({ ...m, colors: updated.length ? updated : undefined }));
  }

  toggleSize(size: string) {
    const current = this.model().sizes ?? [];
    const updated = current.includes(size)
      ? current.filter(s => s !== size)
      : [...current, size];
    this.model.update(m => ({ ...m, sizes: updated.length ? updated : undefined }));
  }
}
