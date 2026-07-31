import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LayoutService } from './services/layout.service';
import { LayoutType } from './types/enums/layout-type';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  const layoutType = signal(LayoutType.Main);

  beforeEach(async () => {
    layoutType.set(LayoutType.Main);
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        {
          provide: LayoutService,
          useValue: { layoutType: layoutType.asReadonly() },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('renders routed content inside the main layout', () => {
    expect(fixture.nativeElement.querySelector('app-main-layout')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-empty-layout')).toBeNull();
    expect(
      fixture.nativeElement.querySelectorAll('router-outlet').length,
    ).toBe(1);
  });

  it('switches routed content to the empty layout reactively', () => {
    layoutType.set(LayoutType.Empty);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-main-layout')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-empty-layout')).not.toBeNull();
    expect(
      fixture.nativeElement.querySelectorAll('router-outlet').length,
    ).toBe(1);
  });

  it('exposes the layout enum used by its template', () => {
    expect(fixture.componentInstance.LayoutType).toBe(LayoutType);
    expect(fixture.componentInstance.layoutType()).toBe(LayoutType.Main);
  });
});
