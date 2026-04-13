import { inject } from '@angular/core';
import { signalStore, withProps } from '@ngrx/signals';
import { AuthStore } from './auth.store';
import { CartStore } from './cart.store';

export const AppStore = signalStore(
  { providedIn: 'root' },

  withProps(() => ({
    auth: inject(AuthStore),
    cart: inject(CartStore),
  })),
);
