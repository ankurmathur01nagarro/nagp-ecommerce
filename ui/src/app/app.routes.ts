import { Routes } from '@angular/router';
import { LoginOrRegisterComponent } from './components/LoginOrRegister/loginOrRegister.component';
import { PageNotFoundComponent } from './components/PageNotFound/page-not-found';
import { HomeComponent } from './components/Home/home-component';
import { CatalogComponent } from './components/catalog-component/catalog-component';
import { WelcomePageComponent } from './components/welcome-page-component/welcome-page-component';
import { FooterComponent } from './components/footer-component/footer-component';

export const routes: Routes = [
  {
    path: 'notloggedin',
    component: LoginOrRegisterComponent
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
      }
    ]
  },
  {
    path: '**',
    component: PageNotFoundComponent
  }
];
