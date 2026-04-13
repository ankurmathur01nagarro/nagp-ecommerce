import { afterNextRender, DestroyRef, Directive, ElementRef, inject, model, effect } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime } from 'rxjs';

@Directive({ selector: '[priceRangeSlider]', standalone: true })
export class PriceRangeSliderDirective {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  /** Two-way bindable current range. Use [(value)]="someSignal" in templates. */
  readonly value = model<{ min: number; max: number }>({ min: 0, max: 0 });

  private readonly slideSubject = new Subject<{ min: number; max: number }>();

  constructor() {
    // Debounced slider drag → write back to the model signal.
    this.slideSubject
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(v => this.value.set(v));

    // When value() is set from outside (e.g. form reset), push it into the jQuery slider.
    effect(() => {
      const { min, max } = this.value();
      const el = this.el.nativeElement;
      const $el = (window as any)['jQuery']?.(el);
      if ($el?.slider('instance')) {
        $el.slider('values', [min, max]);
      }
    });

    afterNextRender({
      write: () => {
        const el = this.el.nativeElement;

        // Initialise the jQuery UI slider on this element only.
        (window as any)['initPriceRangeSlider']?.(el);

        // Watch current handle positions — the slider writes data-currentmin / data-currentmax
        // on every slide event so this observer fires without touching the overall range attrs.
        const currentObserver = new MutationObserver(() => {
          const min = parseFloat(el.getAttribute('data-currentmin') ?? '0');
          const max = parseFloat(el.getAttribute('data-currentmax') ?? '0');
          this.slideSubject.next({ min, max });
        });

        currentObserver.observe(el, {
          attributes: true,
          attributeFilter: ['data-currentmin', 'data-currentmax'],
        });

        // Watch overall range changes (Angular [attr.data-min] / [attr.data-max] bindings)
        // and re-initialise the slider so it reflects the new product price range.
        const rangeObserver = new MutationObserver(() => {
          (window as any)['initPriceRangeSlider']?.(el);
        });

        rangeObserver.observe(el, {
          attributes: true,
          attributeFilter: ['data-min', 'data-max'],
        });

        this.destroyRef.onDestroy(() => {
          currentObserver.disconnect();
          rangeObserver.disconnect();
        });
      },
    });
  }
}
