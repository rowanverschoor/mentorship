import { Component, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '../../models/location';

// Collects coordinates and reports them. Doesn't know what happens next.
@Component({
  selector: 'app-location-form',
  imports: [FormsModule],
  templateUrl: './location-form.component.html',
  styleUrl: './location-form.component.css',
})
export class LocationFormComponent {
  latitude = 52.37;
  longitude = 4.89;

  submitted = output<Location>();

  submit() {
    this.submitted.emit({ latitude: this.latitude, longitude: this.longitude });
  }
}
