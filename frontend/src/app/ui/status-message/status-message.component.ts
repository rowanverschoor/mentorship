import { Component, input, ChangeDetectionStrategy } from '@angular/core';

// Pure display of a loading or error state.
@Component({
  selector: 'app-status-message',
  templateUrl: './status-message.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './status-message.component.css',
})
export class StatusMessageComponent {
  loading = input(false);
  error = input('');
}
