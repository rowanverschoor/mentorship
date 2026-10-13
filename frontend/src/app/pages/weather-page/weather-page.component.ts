import { Component, inject } from '@angular/core';
import { WeatherStore } from '../../state/weather.store';
import { LocationFormComponent } from '../../ui/location-form/location-form.component';
import { StatusMessageComponent } from '../../ui/status-message/status-message.component';
import { WeatherDetailsComponent } from '../../ui/weather-details/weather-details.component';

// Container: connects the store to the dumb UI components. No logic of its own.
@Component({
  selector: 'app-weather-page',
  imports: [LocationFormComponent, StatusMessageComponent, WeatherDetailsComponent],
  templateUrl: './weather-page.component.html',
  styleUrl: './weather-page.component.css',
})
export class WeatherPageComponent {
  protected store = inject(WeatherStore);
}
