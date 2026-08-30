import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TradingModeService {
  colors(mode: string): { background: string; color: string } {
    switch (mode) {
      case 'Live':
        return { background: '#f8d7da', color: '#721c24' }; // danger
      case 'Paper':
        return { background: '#fff3cd', color: '#856404' }; // warning
      default:
        return { background: '#d1ecf1', color: '#0c5460' }; // info
    }
  }
}
