import { CommonModule } from '@angular/common';
import { booleanAttribute, Component, ElementRef, forwardRef, HostListener, Input } from '@angular/core';
import { ControlValueAccessor, NG_VALIDATORS, NG_VALUE_ACCESSOR, ValidationErrors, Validator } from '@angular/forms';

export interface AppSelectOption<T = string | number | null> {
  value: T;
  label: string;
  disabled?: boolean;
}

@Component({
  selector: 'app-select',
  imports: [CommonModule],
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => AppSelectComponent), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => AppSelectComponent), multi: true },
  ],
  template: `
    <div class="app-select" [class.app-select-open]="open" [class.app-select-disabled]="isDisabled">
      <button
        type="button"
        class="app-select-trigger"
        [attr.aria-expanded]="open"
        [attr.aria-label]="ariaLabel || placeholder"
        [disabled]="isDisabled"
        (click)="toggle()"
        (blur)="markTouched()">
        <span [class.app-select-placeholder]="!selectedOption">{{ selectedOption?.label || placeholder }}</span>
        <span class="app-select-chevron" aria-hidden="true"></span>
      </button>

      @if (open) {
        <div class="app-select-panel" role="listbox">
          @for (option of options; track option.value) {
            <button
              type="button"
              role="option"
              class="app-select-option"
              [class.app-select-option-active]="sameValue(option.value, value)"
              [disabled]="option.disabled"
              (click)="choose(option)">
              {{ option.label }}
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .app-select { position: relative; min-width: 0; }
    .app-select-trigger {
      display: flex;
      width: 100%;
      min-height: 44px;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      border: 1px solid var(--select-border,#dbe3eb);
      border-radius: 8px;
      background: var(--select-bg,#fff);
      padding: 10px 12px;
      color: var(--select-text,#334155);
      font: inherit;
      font-size: 14px;
      text-align: left;
      box-shadow: 0 1px 2px rgba(15,23,42,.04);
      transition: border-color .15s, box-shadow .15s, background .15s;
    }
    .app-select-trigger:hover { border-color: #cbd5e1; background: var(--select-hover,#f8fafc); }
    .app-select-open .app-select-trigger {
      border-color: var(--select-accent,#84cc16);
      box-shadow: 0 0 0 4px var(--select-ring,rgba(132,204,22,.14));
    }
    .app-select-placeholder { color: var(--select-placeholder,#64748b); }
    .app-select-chevron {
      width: 8px;
      height: 8px;
      flex: 0 0 auto;
      border-right: 1.8px solid var(--select-arrow,#64748b);
      border-bottom: 1.8px solid var(--select-arrow,#64748b);
      transform: rotate(45deg) translateY(-2px);
      transition: transform .15s;
    }
    .app-select-open .app-select-chevron { transform: rotate(225deg) translate(-1px,-1px); }
    .app-select-panel {
      position: absolute;
      z-index: 260;
      top: calc(100% + 6px);
      right: 0;
      left: 0;
      overflow: auto;
      max-height: 280px;
      border: 1px solid var(--select-panel-border,#dbe3eb);
      border-radius: 8px;
      background: var(--select-panel-bg,#fff);
      padding: 5px;
      box-shadow: 0 18px 42px rgba(15,23,42,.16);
    }
    .app-select-option {
      display: block;
      width: 100%;
      border: 0;
      border-radius: 6px;
      background: transparent;
      padding: 9px 10px;
      color: var(--select-option,#334155);
      font: inherit;
      font-size: 14px;
      line-height: 1.25;
      text-align: left;
      transition: background .12s, color .12s;
    }
    .app-select-option:hover:not(:disabled) { background: var(--select-option-hover,#eef4ff); color: var(--select-option-hover-text,#2563eb); }
    .app-select-option-active { background: var(--select-option-active,#dbeafe); color: var(--select-option-active-text,#2563eb); }
    .app-select-option:disabled { cursor: not-allowed; opacity: .5; }
    .app-select-disabled { opacity: .62; }
    .app-select-disabled .app-select-trigger { cursor: not-allowed; }
    :host-context(.admin-shell) {
      --select-bg: #1f242c;
      --select-hover: #242932;
      --select-border: #3a414d;
      --select-panel-border: #3a414d;
      --select-panel-bg: #1f242c;
      --select-text: #f8fafc;
      --select-placeholder: #aab4c3;
      --select-arrow: #aab4c3;
      --select-ring: rgba(132,204,22,.18);
      --select-option: #f8fafc;
      --select-option-hover: #2d333d;
      --select-option-hover-text: #d9f99d;
      --select-option-active: #334155;
      --select-option-active-text: #d9f99d;
    }
    :host-context(.owner-dark) {
      --select-bg: #1f242c;
      --select-hover: #242932;
      --select-border: #3a414d;
      --select-panel-border: #3a414d;
      --select-panel-bg: #1f242c;
      --select-text: #f8fafc;
      --select-placeholder: #aab4c3;
      --select-arrow: #aab4c3;
      --select-accent: var(--brand,#84cc16);
      --select-ring: color-mix(in srgb,var(--brand,#84cc16) 20%,transparent);
      --select-option: #f8fafc;
      --select-option-hover: #2d333d;
      --select-option-hover-text: color-mix(in srgb,var(--brand,#84cc16) 70%,white);
      --select-option-active: color-mix(in srgb,var(--brand,#84cc16) 18%,#334155);
      --select-option-active-text: color-mix(in srgb,var(--brand,#84cc16) 72%,white);
    }
  `],
})
export class AppSelectComponent implements ControlValueAccessor, Validator {
  @Input() options: AppSelectOption[] = [];
  @Input() placeholder = 'Odaberi';
  @Input() ariaLabel = '';
  @Input({ transform: booleanAttribute }) required = false;
  @Input({ transform: booleanAttribute }) set disabled(value: boolean) {
    this.setDisabledState(value);
  }

  value: AppSelectOption['value'] = null;
  open = false;
  isDisabled = false;

  private onChange: (value: AppSelectOption['value']) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  get selectedOption(): AppSelectOption | undefined {
    return this.options.find((option) => this.sameValue(option.value, this.value));
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.host.nativeElement.contains(event.target as Node)) this.open = false;
  }

  writeValue(value: AppSelectOption['value']): void {
    this.value = value ?? null;
  }

  registerOnChange(fn: (value: AppSelectOption['value']) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.isDisabled = disabled;
    if (disabled) this.open = false;
  }

  validate(): ValidationErrors | null {
    return this.required && (this.value === null || this.value === '') ? { required: true } : null;
  }

  toggle(): void {
    if (!this.isDisabled) this.open = !this.open;
  }

  choose(option: AppSelectOption): void {
    if (option.disabled) return;
    this.value = option.value;
    this.onChange(option.value);
    this.markTouched();
    this.open = false;
  }

  markTouched(): void {
    this.onTouched();
  }

  sameValue(a: AppSelectOption['value'], b: AppSelectOption['value']): boolean {
    return String(a ?? '') === String(b ?? '');
  }
}
