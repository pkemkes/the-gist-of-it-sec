import { useDispatch } from "react-redux";
import { useAppSelector } from "../../store";
import { selectTimezone, timezoneChanged } from "../slice";
import { allTimezones, ITimezone, useTimezoneSelect } from "react-timezone-select";
import { Box, FormControl, IconButton, InputLabel, Menu, MenuItem, Select, SelectChangeEvent } from "@mui/material";
import LanguageIcon from '@mui/icons-material/Language';
import React from "react";

const timeZones = {
  ...allTimezones,
  "Asia/Manila": "Manila",
}

export const parseTimezone = (zone: ITimezone) => {
  const { parseTimezone } = useTimezoneSelect({ labelStyle: "original", timezones: timeZones });
  return parseTimezone(zone);
}

export const TimezoneSelector = () => {
  const dispatch = useDispatch();
  const selectedTimezone = useAppSelector(selectTimezone);

  const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
  const menuOpenend = Boolean(anchorEl);
  const handleMenuClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(event.currentTarget);
  };
  const handleMenuClose = () => {
    setAnchorEl(null);
  };

  const { options, parseTimezone } = useTimezoneSelect({ labelStyle: "original", timezones: timeZones });

  const handleChange = (event: SelectChangeEvent) => {
    dispatch(timezoneChanged(event.target.value));
  }

  return <div>
    <IconButton
      id="menu-button"
      aria-controls={menuOpenend ? 'timezone-menu' : undefined}
      aria-haspopup="true"
      aria-expanded={menuOpenend ? 'true' : undefined}
      onClick={handleMenuClick}
      sx={{ mr: "1rem" }}
    >
      <LanguageIcon />
    </IconButton>
    <Menu
      id="timezone-menu"
      anchorEl={anchorEl}
      open={menuOpenend}
      onClose={handleMenuClose}
      anchorOrigin={{
        vertical: 'bottom',
        horizontal: 'right',
      }}
      transformOrigin={{
        vertical: 'top',
        horizontal: 'right',
      }}
      MenuListProps={{
        'aria-labelledby': 'menu-button',
      }}
    >
      <MenuItem>
        <FormControl>
          <InputLabel id="demo-simple-select-label">Timezone</InputLabel>
          <Select
            labelId="demo-simple-select-label"
            id="demo-simple-select"
            value={parseTimezone(selectedTimezone).value}
            label="Timezone"
            onChange={handleChange}
          >
            { options.map((option, i) => <MenuItem key={ i } value={ option.value }>{ option.label }</MenuItem>) }
          </Select>
        </FormControl>
      </MenuItem>
    </Menu>
  </div>
};