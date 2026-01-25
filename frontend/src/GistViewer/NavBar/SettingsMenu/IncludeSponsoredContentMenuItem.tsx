import { Box, ToggleButton, ToggleButtonGroup, Tooltip, Typography } from "@mui/material";
import { useDispatch } from "react-redux";
import { selectIncludeSponsoredContent, includeSponsoredContentChanged } from "../../slice";
import { useAppSelector } from "../../../store";

export const IncludeSponsoredContentMenuItem = () => {
	const dispatch = useDispatch();
  const selectedIncludeSponsoredContent = useAppSelector(selectIncludeSponsoredContent);
  
  const handleChange = (_: React.MouseEvent<HTMLElement>, newIncludeSponsoredContent: boolean) => {
    dispatch(includeSponsoredContentChanged(newIncludeSponsoredContent));
  }

  return <Box sx={{ px: "1rem", mb: "1rem", display: "flex", alignItems: "center" }} >
    <Typography sx={{ ml: "0.5rem", fontSize: "0.8rem", color: "text.secondary" }}>
      Sponsored Content:
    </Typography>
    <ToggleButtonGroup
      value={ selectedIncludeSponsoredContent }
      exclusive
      onChange={ handleChange }
      aria-label="show sponsored content"
      sx={{ flexGrow: 1, width: "100%" }}
    >
        <Tooltip title="Show sponsored content">
          <ToggleButton sx={{ flex: 1 }} value={true} aria-label="show sponsored content">
            Include
          </ToggleButton>
        </Tooltip>
        <Tooltip title="Hide sponsored content">
          <ToggleButton sx={{ flex: 1 }} value={false} aria-label="hide sponsored content">
            Hide
          </ToggleButton>
        </Tooltip>
      </ToggleButtonGroup>
  </Box>
}