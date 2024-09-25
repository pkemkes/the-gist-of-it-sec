import { Button } from "@mui/material";
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { useNavigate } from "react-router";

export const BackButton = () => {
  const navigate = useNavigate();
  
  return <Button
    onClick={() => {
      navigate("/");
    }}
    variant="outlined"
    startIcon={<ArrowBackIcon />}
    sx={{
      mr: "auto",
      mb: "20px",
      width: "8rem",
    }}
  >
    Back
  </Button>;
}