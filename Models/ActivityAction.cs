namespace MvcMusic.Models
{
    public enum ActivityAction
    {
        // Account & Security
        Login,
        Logout,
        Register,
        ResetPassword,
        
        // Employee Management
        CreateEmployee,
        EditEmployee,
        DeleteEmployee,
        BanEmployee,
        UnbanEmployee,
        
        // Customer Management
        BanCustomer,
        UnbanCustomer,
        DeleteCustomer,
        
        // Product Management
        CreateProduct,
        EditProduct,
        DeleteProduct, // Soft delete
        UpdateProduct,
        UpdateTable,   // Mass update via UpdatableTable
        
        // Order Management
        PlaceOrder,
        DeliverOrder,
        CancelOrder,
        
        // System
        CreateRole,
        SystemSeed
    }
}
