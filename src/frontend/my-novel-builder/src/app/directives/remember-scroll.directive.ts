import { afterEveryRender, Directive, ElementRef, HostListener, inject, Input, OnChanges, OnDestroy } from '@angular/core';

/** Tab-local scroll memory for asynchronously rendered editor content. */
@Directive({ selector: '[appRememberScroll]', standalone: true })
export class RememberScrollDirective implements OnChanges, OnDestroy {
  @Input() appRememberScroll = '';
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private target: number | null = null;
  private observer = new ResizeObserver(() => this.restore());
  private readonly renderRef = afterEveryRender(() => {
    for (const child of Array.from(this.element.children)) {
      this.observer.observe(child);
    }
    this.restore();
  });

  ngOnChanges(): void {
    this.target = 0;
    try {
      const saved = Number(sessionStorage.getItem(this.appRememberScroll));
      if (Number.isFinite(saved) && saved >= 0) this.target = saved;
    } catch {
      // Storage can be disabled by browser settings.
    }
  }

  private restore(): void {
    if (this.target !== null) this.element.scrollTop = this.target;
  }

  @HostListener('wheel')
  @HostListener('touchstart')
  @HostListener('pointerdown')
  @HostListener('keydown')
  stopRestoring(): void {
    this.target = null;
  }

  @HostListener('scroll')
  save(): void {
    // Don't replace the saved position while content is still growing.
    if (this.target !== null) return;
    try {
      sessionStorage.setItem(this.appRememberScroll, String(this.element.scrollTop));
    } catch {
      // Scroll memory is optional when storage is unavailable.
    }
  }

  ngOnDestroy(): void {
    this.observer.disconnect();
    this.renderRef.destroy();
  }
}
