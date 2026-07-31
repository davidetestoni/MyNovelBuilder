import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  NavigationStart,
  Router,
} from '@angular/router';
import { Subject } from 'rxjs';
import { LayoutType } from '../types/enums/layout-type';
import { LayoutService } from './layout.service';

describe('LayoutService', () => {
  let events: Subject<unknown>;
  let rootSnapshot: ActivatedRouteSnapshot;

  const routeSnapshot = (
    layoutType?: LayoutType,
    firstChild: ActivatedRouteSnapshot | null = null,
  ): ActivatedRouteSnapshot =>
    ({
      data:
        layoutType === undefined
          ? {}
          : { layoutType },
      firstChild,
    }) as unknown as ActivatedRouteSnapshot;

  const configure = (
    initialRoot: ActivatedRouteSnapshot,
    platformId: object | string = 'browser',
  ): LayoutService => {
    events = new Subject<unknown>();
    rootSnapshot = initialRoot;
    const router = {
      events: events.asObservable(),
      routerState: {
        snapshot: {
          get root(): ActivatedRouteSnapshot {
            return rootSnapshot;
          },
        },
      },
    } as unknown as Router;

    TestBed.configureTestingModule({
      providers: [
        LayoutService,
        { provide: Router, useValue: router },
        { provide: PLATFORM_ID, useValue: platformId },
      ],
    });

    return TestBed.inject(LayoutService);
  };

  it('uses the deepest active route layout on construction', () => {
    const leaf = routeSnapshot(LayoutType.Main);
    const service = configure(
      routeSnapshot(LayoutType.Empty, routeSnapshot(undefined, leaf)),
    );

    expect(service.layoutType()).toBe(LayoutType.Main);
  });

  it('defaults to the empty layout when route metadata is absent', () => {
    const service = configure(routeSnapshot());

    expect(service.layoutType()).toBe(LayoutType.Empty);
  });

  it('updates the layout after completed browser navigation', () => {
    const service = configure(routeSnapshot());
    rootSnapshot = routeSnapshot(undefined, routeSnapshot(LayoutType.Main));

    events.next(new NavigationEnd(1, '/novels', '/novels'));

    expect(service.layoutType()).toBe(LayoutType.Main);
  });

  it('returns to the empty layout when new route metadata is absent', () => {
    const service = configure(
      routeSnapshot(undefined, routeSnapshot(LayoutType.Main)),
    );
    rootSnapshot = routeSnapshot(undefined, routeSnapshot());

    events.next(new NavigationEnd(2, '/unknown', '/unknown'));

    expect(service.layoutType()).toBe(LayoutType.Empty);
  });

  it('ignores navigation events until navigation completes', () => {
    const service = configure(
      routeSnapshot(undefined, routeSnapshot(LayoutType.Main)),
    );
    rootSnapshot = routeSnapshot();

    events.next(new NavigationStart(1, '/other'));

    expect(service.layoutType()).toBe(LayoutType.Main);
  });

  it('does not subscribe to router navigation during server rendering', () => {
    const service = configure(
      routeSnapshot(undefined, routeSnapshot(LayoutType.Main)),
      'server',
    );
    rootSnapshot = routeSnapshot();

    events.next(new NavigationEnd(1, '/other', '/other'));

    expect(service.layoutType()).toBe(LayoutType.Main);
  });
});
