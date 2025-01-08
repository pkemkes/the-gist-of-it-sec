import { useDispatch } from "react-redux";
import { useAppSelector } from "../../../store";
import { selectTimezone, timezoneChanged } from "../../slice";
import { allTimezones, useTimezoneSelect } from "react-timezone-select";
import { FormControl, InputLabel, MenuItem, Select, SelectChangeEvent } from "@mui/material";
import { ShortenedTypography } from "./ShortenedTypography";


const ITEM_HEIGHT = 48;
const ITEM_PADDING_TOP = 8;
const MenuProps = {
  PaperProps: {
    style: {
      maxHeight: ITEM_HEIGHT * 4.5 + ITEM_PADDING_TOP,
      width: 250,
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

	const { options } = useTimezoneSelect({ labelStyle: "original", timezones: timeZones });

  const handleChange = (event: SelectChangeEvent) => {
    dispatch(timezoneChanged(event.target.value));
  }

  return <MenuItem>
    <FormControl sx={{ my: "0.5rem", width: "20rem" }}>
      <InputLabel id="demo-simple-select-label">Timezone</InputLabel>
      <Select
        labelId="demo-simple-select-label"
        id="demo-simple-select"
        value={ selectedTimezone }
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