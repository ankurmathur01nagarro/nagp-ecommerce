import { Routes } from '@angular/router';
import { LoginOrRegisterComponent } from './components/LoginOrRegister/loginOrRegister.component';
import { alreadyLoggedInGuard } from './guards/already-logged-in.guard';
import { PageNotFoundComponent } from './components/PageNotFound/page-not-found';
import { HomeComponent } from './components/Home/home-component';
import { CatalogComponent } from './components/catalog-component/catalog-component';
import { WelcomePageComponent } from './components/welcome-page-component/welcome-page-component';
import { FooterComponent } from './components/footer-component/footer-component';
import { AuthorizationHandlerComponent } from './components/authorization-handler/authorization-handler';
import { ProductDetailComponent } from './components/product-detail/product-detail';

export const routes: Routes = [
  {
    path: 'notloggedin',
    component: LoginOrRegisterComponent,
    canActivate: [alreadyLoggedInGuard]
  },
  {
    path: 'auth/callback',
    component: AuthorizationHandlerComponent
  },
  {
    path: '',
    component: HomeComponent,
    children: [
      {
        path: '',
        component: WelcomePageComponent,
      },
      {
        path: '',
        component: FooterComponent,
        outlet: 'footer',
        pathMatch: 'full'
      },
      {
        path: 'catalog',
        component: CatalogComponent
      },
      {
        path: 'catalog/:catId',
        component: CatalogComponent
      },
      {
        path: 'product/:id',
        component: ProductDetailComponent
      }
    ]
  },
  {
    path: '**',
    component: PageNotFoundComponent
  }
];
