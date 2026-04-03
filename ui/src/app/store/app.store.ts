import { inject } from '@angular/core';
import { signalStore, withProps } from '@ngrx/signals';
import { AuthStore } from './auth.store';

export const AppStore = signalStore(
  { providedIn: 'root' },

  withProps(() => ({
    auth: inject(AuthStore),
    // future stores: cart: inject(CartStore), product: inject(ProductStore), etc.
  })),
);
