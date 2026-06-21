import { Injectable } from '@angular/core';
import QRCode from 'qrcode';

@Injectable({ providedIn: 'root' })
export class QrCodeService {
  createDataUrl(value: string): Promise<string> {
    return QRCode.toDataURL(value, {
      errorCorrectionLevel: 'H',
      margin: 2,
      width: 420,
      color: { dark: '#111827', light: '#ffffff' },
    });
  }
}
