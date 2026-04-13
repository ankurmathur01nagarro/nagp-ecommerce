import { computed, inject } from '@angular/core';
import { UserService, type CartDetails } from '@app/services/user-service';
import { patchState, signalStore, withComputed, withHooks, withMethods, withProps, withState } from '@ngrx/signals';
import { lastValueFrom } from 'rxjs';

type CartState = CartDetails & {
  isLoading: boolean;
  cartError: string | null;
}

const initialState: CartState = {
  items: [],
  totalPrice: 0,
  isLoading: false,
  cartError: null,
};

export const CartStore = signalStore(
  { providedIn: 'root' },

  withState(initialState),

  withProps(() => ({
    _userService: inject(UserService),
  })),

  withComputed(({ items }) => ({
    totalPrice: computed(() => items().reduce((total, item) => total + item.price * item.quantity, 0)),
  })),

  withMethods((store) => ({
    async addToCart(productId: string): Promise<void> {
      patchState(store, { isLoading: true, cartError: null });
      try {
        const cartDetails = await lastValueFrom(store._userService.addToCart(productId));
        patchState(store, { ...cartDetails, isLoading: false });
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : 'Add to cart failed. Please try again.';
        patchState(store, { isLoading: false, cartError: message });
      }
    },

    async removeFromCart(productId: string): Promise<void> {
      patchState(store, { isLoading: true, cartError: null });
      try {
        const cartDetails = await lastValueFrom(store._userService.removeFromCart(productId));
        patchState(store, { ...cartDetails, isLoading: false });
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : 'Remove from cart failed. Please try again.';
        patchState(store, { isLoading: false, cartError: message });
      }
    },

    async clearCart(): Promise<void> {
      patchState(store, { isLoading: true, cartError: null });
      try {
        const cartDetails = await lastValueFrom(store._userService.clearCart());
        patchState(store, { ...cartDetails, isLoading: false });
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : 'Clear cart failed. Please try again.';
        patchState(store, { isLoading: false, cartError: message });
      }
    },

    clearCartError(): void {
      patchState(store, { cartError: null });
    },

  })),

  // withHooks((store) => ({
  //   onInit: () => {
  //     // Optionally, load initial cart details here if needed
  //   }
  // })),
);
