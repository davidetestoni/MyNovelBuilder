import { LayoutType } from './types/enums/layout-type';
import { routes } from './app.routes';

describe('application routes', () => {
  it('redirects the root path to the novels page', () => {
    expect(routes[0]).toEqual(
      jasmine.objectContaining({
        path: '',
        redirectTo: 'novels',
        pathMatch: 'full',
      }),
    );
  });

  it('defines every expected application path exactly once', () => {
    const paths = routes.map((route) => route.path);

    expect(paths).toEqual([
      '',
      'novels',
      'chat',
      'chat/:id',
      'world-builder',
      'world-builder/:id',
      'compendia',
      'novel/:id',
      'novel/:id/settings',
      'novel/:id/planner',
      'compendium/:id',
      'compendium/:id/record/:recordId',
      'prompts',
      'media-library',
      'integrations',
      'voices',
      'about',
    ]);
    expect(new Set(paths).size).toBe(paths.length);
  });

  it('assigns the main layout to every page route', () => {
    const pageRoutes = routes.filter((route) => route.loadComponent);

    expect(pageRoutes.length).toBe(routes.length - 1);
    for (const route of pageRoutes) {
      expect(route.data?.['layoutType']).toBe(LayoutType.Main);
    }
  });

  it('lazy-loads every page component', () => {
    for (const route of routes.slice(1)) {
      expect(route.loadComponent).toEqual(jasmine.any(Function));
      expect(route.component).toBeUndefined();
    }
  });
});
