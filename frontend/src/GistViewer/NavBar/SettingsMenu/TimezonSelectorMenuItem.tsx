import { useDispatch } from "react-redux";
import { useAppSelector } from "../../../store";
import { selectTimezone, timezoneChanged } from "../../slice";
import { allTimezones, useTimezoneSelect } from "react-timezone-select/hook";
import { FormControl, InputLabel, MenuItem, Select, SelectChangeEvent } from "@mui/material";
import { ShortenedTypography } from "./ShortenedTypography";


const ITEM_HEIGHT = 48;
const ITEM_PADDING_TOP = 8;
const MenuProps = {
  slotProps: {
    paper: {
      style: {
        maxHeight: ITEM_HEIGHT * 4.5 + ITEM_PADDING_TOP,
        width: 250,
      },
    },
  },
};

const timeZones = {
	...allTimezones,
	"Asia/Manila": "Manila",
}

export const TimezoneSelectorMenuItem = () => {
	const dispatch = useDispatch();
	const selectedTimezone = useAppSelector(selectTimezone);

	const { parseTimezone } = useTimezoneSelect({ labelStyle: "original", timezones: timeZones });
  // Version 4 groups its options by UTC offset. Keep the existing named choices,
  // including the saved timezone, when rendering our own Material UI select.
  const options = Object.keys(timeZones)
    .map(zone => parseTimezone(zone))
    .filter(option => option != null)
    .sort((a, b) => (a.offset ?? 0) - (b.offset ?? 0));

  const handleChange = (event: SelectChangeEvent) => {
    dispatch(timezoneChanged(event.target.value));
  }

  return <MenuItem disableRipple disableTouchRipple>
    <FormControl sx={{ my: "0.5rem", width: "20rem" }}>
      <InputLabel id="timezone-select-label">Timezone</InputLabel>
      <Select
        labelId="timezone-select-label"
        id="timezone-select"
        value={ parseTimezone(selectedTimezone).value }
        label="Timezone"
        onChange={ handleChange }
        MenuProps={ MenuProps }
      >
        { 
          options.map((option, i) => 
            <MenuItem key={ i } value={ option.value }>
              <ShortenedTypography value={ option.label } />
            </MenuItem>)
        }
      </Select>
    </FormControl>
  </MenuItem>
}
