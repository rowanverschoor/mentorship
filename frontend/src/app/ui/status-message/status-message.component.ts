import { Component, input } from '@angular/core';

// Pure display of a loading or error state.
@Component({
  selector: 'app-status-message',
  templateUrl: './status-message.component.html',
  styleUrl: './status-message.component.css',
})
export class StatusMessageComponent {
  loading = input(false);
  error = input('');
}
