import { Component, Inject, OnInit, PLATFORM_ID } from '@angular/core';
import { Router, RouterOutlet, RoutesRecognized } from '@angular/router';
import { EmptyLayoutComponent } from './layouts/empty-layout/empty-layout.component';
import { MainLayoutComponent } from './layouts/main-layout/main-layout.component';
import { LayoutType } from './types/enums/layout-type';
import { isPlatformBrowser } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  imports: [RouterOutlet, EmptyLayoutComponent, MainLayoutComponent],
})
export class AppComponent implements OnInit {
  LayoutType = LayoutType;
  layoutType: LayoutType = LayoutType.Empty;

  constructor(
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.router.events.subscribe({
        next: (event: any) => {
          if (event instanceof RoutesRecognized) {
            this.layoutType = event.state.root.firstChild?.data['layoutType'];
          }
        },
      });
    }
  }
}
