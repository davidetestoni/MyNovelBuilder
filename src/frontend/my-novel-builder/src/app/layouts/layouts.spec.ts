import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EmptyLayoutComponent } from './empty-layout/empty-layout.component';
import { MainLayoutHeaderComponent } from './main-layout/main-layout-header/main-layout-header.component';
import { MainLayoutComponent } from './main-layout/main-layout.component';

describe('application layouts', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [
        EmptyLayoutComponent,
        MainLayoutComponent,
        MainLayoutHeaderComponent,
      ],
      providers: [provideRouter([])],
    });
  });

  it('renders the empty-layout content container', () => {
    const fixture = TestBed.createComponent(EmptyLayoutComponent);
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector('.empty-layout-container'),
    ).not.toBeNull();
  });

  it('renders the main header and page-content container', () => {
    const fixture = TestBed.createComponent(MainLayoutComponent);
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector('app-main-layout-header'),
    ).not.toBeNull();
    expect(
      fixture.nativeElement.querySelector('.page-content'),
    ).not.toBeNull();
  });

  it('uses semantic links for the logo and all navigation items', () => {
    const fixture = TestBed.createComponent(MainLayoutHeaderComponent);
    fixture.detectChanges();
    const links = Array.from(
      fixture.nativeElement.querySelectorAll('a'),
    ) as HTMLAnchorElement[];

    expect(links.length).toBe(9);
    expect(links.every((link) => link.hasAttribute('href'))).toBeTrue();
    expect(
      fixture.nativeElement.querySelectorAll('.navigation .nav-item').length,
    ).toBe(7);
  });

  it('links each header item to its expected destination', () => {
    const fixture = TestBed.createComponent(MainLayoutHeaderComponent);
    fixture.detectChanges();
    const links = Array.from(
      fixture.nativeElement.querySelectorAll('a'),
    ) as HTMLAnchorElement[];

    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/',
      '/about',
      '/novels',
      '/chat',
      '/world-builder',
      '/compendia',
      '/prompts',
      '/media-library',
      '/integrations',
    ]);
    expect(links[0].textContent).toContain('My Novel Builder');
    expect(links[1].textContent?.trim()).toBe(
      `v${fixture.componentInstance.version} ${fixture.componentInstance.releaseLabel}`,
    );
    expect(links.slice(2).map((link) => link.textContent?.trim())).toEqual([
      'Novels',
      'Chat',
      'World Builder',
      'Compendia',
      'Prompts',
      'Media',
      'Integrations',
    ]);
  });

  it('shows the current version beside the logo', () => {
    const fixture = TestBed.createComponent(MainLayoutHeaderComponent);
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('.version-badge');

    expect(badge.textContent.trim()).toBe(
      `v${fixture.componentInstance.version} ${fixture.componentInstance.releaseLabel}`,
    );
  });

  it('provides descriptive alternative text for the logo image', () => {
    const fixture = TestBed.createComponent(MainLayoutHeaderComponent);
    fixture.detectChanges();
    const logo = fixture.nativeElement.querySelector(
      '.logo img',
    ) as HTMLImageElement;

    expect(logo.alt).toBe('My Novel Builder Logo');
  });
});
