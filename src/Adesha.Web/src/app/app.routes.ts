import { Routes } from '@angular/router';
import { authGuard, loginGuard, setupGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/login',
    pathMatch: 'full',
  },
  {
    path: 'setup',
    canActivate: [setupGuard],
    loadComponent: () => import('./setup/setup.component').then((m) => m.SetupComponent),
  },
  {
    path: 'login',
    canActivate: [loginGuard],
    loadComponent: () => import('./login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./dashboard/dashboard.component').then((m) => m.DashboardComponent),
  },
  {
    path: '**',
    redirectTo: '/login',
  },
];
