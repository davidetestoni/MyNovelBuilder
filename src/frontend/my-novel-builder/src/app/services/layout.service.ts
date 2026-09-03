import { inject, Injectable, PLATFORM_ID, Signal, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { isPlatformBrowser } from '@angular/common';
import { filter, map, startWith } from 'rxjs';
import { LayoutType } from '../types/enums/layout-type';
import { AppRouteData } from '../types/router/app-route-data';

@Injectable({
  providedIn: 'root',
})
export class LayoutService {
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);

  readonly layoutType: Signal<LayoutType>;

  constructor() {
    const initialLayout = this.resolveLayoutType();

    if (!this.isBrowser) {
      this.layoutType = signal(initialLayout);
      return;
    }

    this.layoutType = toSignal(
      this.router.events.pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        map(() => this.resolveLayoutType()),
        startWith(initialLayout),
      ),
      { initialValue: initialLayout },
    );
  }

  private resolveLayoutType(): LayoutType {
    let currentRoute: ActivatedRouteSnapshot | null = this.router.routerState.snapshot.root;
    while (currentRoute?.firstChild) {
      currentRoute = currentRoute.firstChild;
    }

    const routeData = currentRoute?.data as AppRouteData | undefined;
    return routeData?.layoutType ?? LayoutType.Empty;
  }
}
