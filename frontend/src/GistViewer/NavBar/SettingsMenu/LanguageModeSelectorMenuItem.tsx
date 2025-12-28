import { useDispatch } from "react-redux";
import { useAppSelector } from "../../../store";
import { selectLanguageMode, languageModeChanged } from "../../slice";
import { FormControl, InputLabel, MenuItem, Select, SelectChangeEvent } from "@mui/material";
import { LanguageMode } from "../../../types";
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

export const LanguageModeSelectorMenuItem = () => {
	const dispatch = useDispatch();
	const selectedLanguageMode = useAppSelector(selectLanguageMode);

  const handleChange = (event: SelectChangeEvent) => {
    dispatch(languageModeChanged(event.target.value as LanguageMode));
  }

  return <MenuItem disableRipple disableTouchRipple>
    <FormControl sx={{ my: "0.5rem", width: "20rem" }}>
      <InputLabel id="language-mode-select-label">Language Mode</InputLabel>
      <Select
        labelId="language-mode-select-label"
        id="language-mode-select"
        value={ selectedLanguageMode }
        label="Language Mode"
        onChange={ handleChange }
        MenuProps={ MenuProps }
      >
		<MenuItem value={ LanguageMode.ORIGINAL }>
			<ShortenedTypography value="Original" />
		</MenuItem>
		<MenuItem value={ LanguageMode.ENGLISH }>
			<ShortenedTypography value="English" />
		</MenuItem>
		<MenuItem value={ LanguageMode.GERMAN }>
			<ShortenedTypography value="Deutsch" />
		</MenuItem>
      </Select>
    </FormControl>
  </MenuItem>
}