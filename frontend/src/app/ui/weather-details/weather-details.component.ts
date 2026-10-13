import { Component, input } from '@angular/core';
import { Weather } from '../../models/weather';

// Pure display: data in, markup out. No services.
@Component({
  selector: 'app-weather-details',
  templateUrl: './weather-details.component.html',
  styleUrl: './weather-details.component.css',
})
export class WeatherDetailsComponent {
  weather = input.required<Weather>();
}
