import { Box, Button } from "@mui/material"
import { useNavigate } from "react-router";

export const RecapButtons = () => {
	const navigate = useNavigate();

	return <Box sx={{ 
			display: "grid",
			gridTemplateColumns: "1fr 1fr",
			columnGap: "1rem",
			// padding: "1rem",
			mb: "1rem",
		}}>
		<Button 
		  variant="outlined" 
		//   size="large"
		  onClick={ () => { 
			navigate("/?recap=daily");
			document.documentElement.scrollTop = 0;
		  }}
		>
			Recap 24 hours
		</Button>
		<Button 
		  variant="outlined" 
		//   size="large"
		  onClick={ () => { 
			navigate("/?recap=weekly");
			document.documentElement.scrollTop = 0;
		  }}
		>
			Recap 7 days
		</Button>
	</Box>
}