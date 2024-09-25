import { InputAdornment, TextField, useTheme } from "@mui/material"
import SearchIcon from '@mui/icons-material/Search';
import { useAppDispatch } from "../../store";
import { searchQueryChanged } from "../slice";
import { useEffect, useState } from "react";

export const SearchBar = () => {
  const [ searchQuery, setSearchQuery ] = useState("");
  const dispatch = useAppDispatch();
  const isLightMode = useTheme().palette.mode == "light";

  useEffect(() => {
    const debounceTimer = setTimeout(() => {
      dispatch(searchQueryChanged(searchQuery));
    }, 250);
    return () => {
      clearTimeout(debounceTimer);
    }
  }, [searchQuery]);

  const handleChange = (event: React.FormEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    setSearchQuery(event.currentTarget.value);
  };

	return <TextField 
		label="Search"
    size="small"
    sx={{ mr: "0.5rem", width: "20rem" }}
    value={ searchQuery }
    onChange={ handleChange }
    color={ isLightMode ? "secondary" : "primary" }
    slotProps={{
      input: {
        endAdornment: (
          <InputAdornment position="end">
            <SearchIcon />
          </InputAdornment>
        ),
      }
    }}
   />
}